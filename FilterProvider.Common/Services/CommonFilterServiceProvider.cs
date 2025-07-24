/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeil.Core.Windows.Util;
using CloudVeil.Core.Windows.Util.Update;
using CloudVeil.IPC;
using CloudVeil.IPC.Messages;
using Filter.Platform.Common.Data.Models;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Gui.CloudVeil.Util;

using Filter.Platform.Common.Util;
using Filter.Platform.Common.Extensions;
using FilterProvider.Common.Platform;
using FilterProvider.Common.Configuration;
using FilterProvider.Common.Util;
using Filter.Platform.Common;
using FilterProvider.Common.Data;
using Filter.Platform.Common.Net;
using Filter.Platform.Common.Types;
using NodaTime;
using GoproxyWrapper;
using FilterProvider.Common.Data.Filtering;
using FilterProvider.Common.ControlServer;
using FilterProvider.Common.Proxy.Certificate;
using Org.BouncyCastle.Crypto;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Filter.Platform.Common.IPC.Messages;
using HandlebarsDotNet;
using CloudVeil;
using GoProxyWrapper;
using System.Diagnostics.Eventing.Reader;
using Sentry;

namespace FilterProvider.Common.Services
{
    /// <summary>
    /// This is an optional delegate that the common filter services provider can call after its services are initialized.
    /// Use this for platform-specific behaviors. Not intended to replace PlatformTypes.
    /// </summary>
    /// <param name="provider"></param>
    public delegate void ExtensionDelegate(CommonFilterServiceProvider provider);
    
    public delegate void PortsChangedDelegate();

    /// <summary>
    /// This is an optional delegate that the common filter services provider can call to determine the version of the program that is running it.
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    public delegate Version VersionDelegate(CommonFilterServiceProvider provider);

    /// <summary>
    /// FilterProvider.Common and CommonFilterServiceProvider are intended to be the cross-platform parts of our filter. You should be able to take FilterProvider.Common and wrap it in
    /// a windows service, a MacOS app bundle, or a Linux executable and have it authenticate against the API, download rules, and filter.
    /// </summary>
    /// <remarks>
    /// <seealso cref="PlatformTypes"/> This is an integral part of our cross-platform implementation. If there is an OS-specific implementation of something,
    /// we use this to register an interface for it, so that we can instantiate the interface in cross-platform code.
    /// 
    /// <seealso cref="ExtensionDelegate"/> This is a more recent addition. The current use for it is to add Windows-only capabilities, such as conflict detection logic.
    /// We may want to migrate some uses of <see cref="ISystemServices"/> to this ExtensionDelegate system.
    ///
    /// </remarks>
    public class CommonFilterServiceProvider
    {
        #region Windows Service API

        private bool isTestRun = false;

        /// <summary>
        /// Starts the common filter service provider logic.
        /// </summary>
        /// <param name="isTest"></param>
        /// 
        /// <returns></returns>
        public bool Start(bool isTest)
        {
            isTestRun = isTest;

            Thread thread = new Thread(OnStartup);
            thread.Start();

            return true;
        }

        public bool Stop()
        {
            logger.Info("FilterServiceProvider stop called");
            // We always return false because we don't let anyone tell us that we're going to stop.
            return false;
        }

        public bool Shutdown()
        {
            logger.Info("FilterServiceProvider shutdown called");
            // Called on a shutdown event.
            Environment.Exit((int)ExitCodes.ShutdownWithSafeguards);
            return true;
        }


        public static void StartSentry()
        {
            var sentry = SentrySdk.Init(o =>
            {
                o.Dsn = new Dsn(CloudVeil.CompileSecrets.SentryDsn);

                o.BeforeBreadcrumb = (breadcrumb) =>
                {
                    if (breadcrumb.Message.Contains("Request"))
                    {
                        return null;
                    }
                    else
                    {
                        return breadcrumb;
                    }
                };
            });
            LoggerUtil.GetAppWideLogger().Info("StartSentry");
        }

        public static void StopSentry()
        {
            SentrySdk.Close();
            LoggerUtil.GetAppWideLogger().Info("StopSentry");
        }


        public void OnSessionChanged()
        {
            systemServices.EnsureGuiRunning(runInTray: true);
        }

        #endregion Windows Service API

        const int RETRIEVE_TOKEN_TIMEOUT = 5000;

        private FilterStatus Status
        {
            get
            {
                try
                {
                    currentStatusLock.EnterReadLock();

                    return currentStatus;
                }
                finally
                {
                    currentStatusLock.ExitReadLock();
                }
            }

            set
            {
                try
                {
                    currentStatusLock.EnterWriteLock();

                    currentStatus = value;
                }
                finally
                {
                    currentStatusLock.ExitWriteLock();
                }

                ipcServer.NotifyStatus(currentStatus);
            }
        }

        private int ConnectedClients
        {
            get
            {
                return Interlocked.CompareExchange(ref connectedClients, connectedClients, 0);
            }

            set
            {
                Interlocked.Exchange(ref connectedClients, value);
            }
        }

        /// <summary>
        /// Our current filter status. 
        /// </summary>
        private FilterStatus currentStatus = FilterStatus.Synchronizing;

        /// <summary>
        /// Our status lock. 
        /// </summary>
        private ReaderWriterLockSlim currentStatusLock = new ReaderWriterLockSlim();

        /// <summary>
        /// The number of IPC clients connected to this server. 
        /// </summary>
        private int connectedClients = 0;

        #region FilteringEngineVars

        private IPCServer ipcServer;

        public IPCServer IPCServer => ipcServer;
        /// <summary>
        /// Used for synchronization whenever our NLP model gets updated while we're already initialized. 
        /// </summary>
        private ReaderWriterLockSlim doccatSlimLock = new ReaderWriterLockSlim();

#if WITH_NLP
        private List<CategoryMappedDocumentCategorizerModel> documentClassifiers = new List<CategoryMappedDocumentCategorizerModel>();
#endif

        private IProxyServer filteringEngine;

        private BackgroundWorker filterEngineStartupBgWorker;

        private static readonly DateTime epoch = new DateTime(1970, 1, 1);

        private static readonly string epochHttpDateTime = epoch.ToString("r");

        /// <summary>
        /// Applications we never ever want to filter. Right now, this is just OS binaries. 
        /// </summary>
        private static readonly HashSet<string> foreverWhitelistedApplications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        #endregion FilteringEngineVars

        private ReaderWriterLockSlim updateRwLock = new ReaderWriterLockSlim();

        private UpdateSystem updateSystem;

        /// <summary>
        /// Timer used to query for filter list changes every X minutes, as well as application updates. 
        /// </summary>
        private Timer updateCheckTimer;

        /// <summary>
        /// Timer used to cleanup logs every 12 hours.
        /// </summary>
        private Timer cleanupLogsTimer;

        /// <summary>
        /// Keep track of the last time we printed the username of the current user so we can output it
        /// to the diagnostics log.
        /// </summary>
        private DateTime lastUsernamePrintTime = DateTime.MinValue;

        /// <summary>
        /// Since clean shutdown can be called from a couple of different places, we'll use this and
        /// some locks to ensure it's only done once.
        /// </summary>
        private volatile bool cleanShutdownComplete = false;

        /// <summary>
        /// Used to ensure clean shutdown once. 
        /// </summary>
        private Object cleanShutdownLock = new object();

        /// <summary>
        /// Logger. 
        /// </summary>
        private Logger logger;

        /// <summary>
        /// This BackgroundWorker object handles initializing the application off the UI thread.
        /// Allows the splash screen to function.
        /// </summary>
        private BackgroundWorker backgroundInitWorker;

        /// <summary>
        /// App function config file. 
        /// </summary>
        IPolicyConfiguration policyConfiguration;

        public IPolicyConfiguration PolicyConfiguration
        {
            get
            {
                return policyConfiguration;
            }
        }

        /// <summary>
        /// The class containing the relaxed policy logic.
        /// </summary>
        RelaxedPolicy relaxedPolicy;

        /// <summary>
        /// This int stores the number of block actions that have elapsed within the given threshold timespan.
        /// </summary>
        private long thresholdTicks;

        /// <summary>
        /// This timer resets the threshold tick count. 
        /// </summary>
        private Timer thresholdCountTimer;

        /// <summary>
        /// This timer is used when the threshold has been hit. It is used to set an expiry period
        /// for the internet lockout once the threshold has been hit.
        /// </summary>
        private Timer thresholdEnforcementTimer;

        /// <summary>
        /// Allows us to periodically check status of time restrictions.
        /// </summary>
        private Timer timeRestrictionsTimer;
        private Timer retrieveTokenTimer;

        private SiteFiltering siteFiltering;

        private DnsEnforcement dnsEnforcement;

        private Accountability accountability;

        private TimeDetection timeDetection;

        private IPlatformTrust trustManager;

        private CertificateExemptions certificateExemptions = new CertificateExemptions();

        private Server controlServer;

        private IPathProvider platformPaths;

        private ISystemServices systemServices;

        private ExtensionDelegate extensionDelegate;

        public event PortsChangedDelegate OnPortsChanged;

        /// <summary>
        /// Default ctor. 
        /// </summary>
        public CommonFilterServiceProvider(ExtensionDelegate extensionDelegate)
        {
            trustManager = PlatformTypes.New<IPlatformTrust>();
            platformPaths = PlatformTypes.New<IPathProvider>();
            systemServices = PlatformTypes.New<ISystemServices>();
            this.extensionDelegate = extensionDelegate;
        }

        /// <summary>
        /// Explicitly defining an object so that we don't need a reference to Microsoft.CSharp.
        /// Xamarin.Mac includes Microsoft.CSharp 2.0.5.0, and the lowest one we can get is Microsoft.CSharp.4.0.0
        /// </summary>
        private class JsonAuthData
        {
            public string authToken { get; set; }
            public string userEmail { get; set; }
        }

        private void onRetrieveTokenTimeout(object state)
        {
            if(RetrieveToken())
            {
                onAuthSuccess();
                retrieveTokenTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        public bool RetrieveToken()
        {
            HttpStatusCode status;
            byte[] tokenResponse = WebServiceUtil.Default.RequestResource(ServiceResource.RetrieveToken, out status);
            if (tokenResponse != null && status == HttpStatusCode.OK)
            {
                try
                {
                    string jsonText = Encoding.UTF8.GetString(tokenResponse);
                    JsonAuthData jsonData = JsonConvert.DeserializeObject<JsonAuthData>(jsonText);

                    WebServiceUtil.Default.AuthToken = jsonData.authToken;
                    WebServiceUtil.Default.UserEmail = jsonData.userEmail;
                    return true;
                }
                catch
                {

                }
            } // else let them continue. They'll have to enter their password if this if isn't taken.
            return false;
        }

        private void RestartFiltering()
        {
            StopFiltering();
            controlServer.Dispose(); 
            InitEngine();
        }

        bool exiting = false;
        private void MonitoringThread()
        {
            var process = Process.GetCurrentProcess();
            while(!exiting)
            {
                try
                {
                    PerformanceCounter pc = new PerformanceCounter();
                    pc.CategoryName = "Process";
                    pc.CounterName = "Working Set - Private";
                    pc.InstanceName = process.ProcessName;
                    var memsize = Convert.ToInt32(pc.NextValue()) / (int)(1024);
                    pc.Close();
                    pc.Dispose(); 
                    if (logger != null)
                    {
                        logger.Info("MEMORY CONSUMPTION IS " + memsize + "KB");
                    }
                }
                catch(Exception e) {
                    if(logger != null) { 
                        logger.Error("Can't get mem counter");
                        logger.Error(e);
                    }
                }
               
                Thread.Sleep(30000);
            }
        }

        private CertificateExemptionsController createControlServerCertificateExemptionsController()
        {
            return new CertificateExemptionsController(certificateExemptions);
        }
        private RelaxedPolicyController createControlServerRelaxedPolicyController()
        {
            return new RelaxedPolicyController(relaxedPolicy);
        }

        private void OnStartup()
        {
            if (File.Exists("debug-filterserviceprovider"))
            {
                Debugger.Launch();
            }

            exiting = false;
            new Thread(MonitoringThread).Start();

            // We spawn a new thread to initialize all this code so that we can start the service and return control to the Service Control Manager.
            bool consoleOutStatus = false;

            try
            {
                // I have reason to suspect that on some 1803 computers, this statement (or some of this initialization) was hanging, causing an error.
                // on service control manager.
                logger = LoggerUtil.GetAppWideLogger();
            }
            catch (Exception ex)
            {
                File.AppendAllText(Path.Combine(platformPaths.ApplicationDataFolder, "FatalCrashLog.log"), $"Fatal crash. {ex.ToString()}");
            }

            try
            {
                Console.SetOut(new ConsoleLogWriter());
                Console.SetError(new ConsoleLogWriter("error"));

                consoleOutStatus = true;
            }
            catch (Exception)
            {
                // Swallow exceptions.
            }

            try
            {
                IPathProvider paths = PlatformTypes.New<IPathProvider>();
                DateTime currentDate = DateTime.Now.Date;
                string dirPath = paths.GetPath("logs");

                GoProxy.Instance.SetProxyLogFile(paths.GetPath("logs", $"proxy-{currentDate.ToString("MM-dd-yyyy")}.log"));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Unable to set proxy log file.");
            }

            string appVerStr = System.Diagnostics.Process.GetCurrentProcess().ProcessName;

            IVersionProvider versionProvider = PlatformTypes.New<IVersionProvider>();
            Version version = versionProvider?.GetApplicationVersion();

            if(version == null)
            {
                Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                version = AssemblyName.GetAssemblyName(assembly.Location).Version;
            }

            appVerStr += " " + version.ToString(3);
            appVerStr += " " + (Environment.Is64BitProcess ? "x64" : "x86");

            logger.Info("CloudVeilService Version: {0}", appVerStr);

            try
            {
                ipcServer = new IPCServer();
                policyConfiguration = new DefaultPolicyConfiguration(ipcServer, logger);
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
                return;
            }

            if (!consoleOutStatus)
            {
                logger.Warn("Failed to link console output to file.");
            }

            // Enforce good/proper protocols
            ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & ~SecurityProtocolType.Ssl3) | (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);

            // Load authtoken and email data from files.
            if (WebServiceUtil.Default.AuthToken == null)
            {
                RetrieveToken();
            }

            // Hook the shutdown/logoff event.
            systemServices.SessionEnding += OnAppSessionEnding;
            //SystemEvents.SessionEnding += OnAppSessionEnding;

            systemServices.OnStartProxy += (sender, e) =>
            {
                try
                {
                    BCCertificateMaker maker = new BCCertificateMaker();

                    AsymmetricCipherKeyPair keyPair = BCCertificateMaker.CreateKeyPair(2048);

                    X509Certificate2 cert = maker.MakeCertificate("localhost", false, systemServices.RootCertificate, keyPair, alternateNames: new Asn1Encodable[] {
                        new GeneralName(GeneralName.DnsName, "localhost"),
                        new GeneralName(GeneralName.IPAddress, "127.0.0.1"),
                        new GeneralName(GeneralName.IPAddress, "::1")
                        });

                    controlServer = new Server(AppSettings.Default.ConfigServerPort, cert);
                    controlServer.RegisterController(typeof(CertificateExemptionsController), createControlServerCertificateExemptionsController);
                    controlServer.RegisterController(typeof(RelaxedPolicyController), createControlServerRelaxedPolicyController);
                    controlServer.Start();
                }
                catch(Exception ex)
                {
                    logger.Error(ex, "An error occurred while attempting to start the control server. " + ex.Message);
                }
            };

//            Swan.Terminal. += Terminal_OnLogMessageReceived;

            // Hook app exiting function. This must be done on this main app thread.
            AppDomain.CurrentDomain.ProcessExit += OnApplicationExiting;

            try
            {
                updateSystem = new UpdateSystem(policyConfiguration, ipcServer, "cv4w");
            }
            catch (Exception e)
            {
                // This is a critical error. We cannot recover from this.
                logger.Error("Critical error - Could not create application updater.");
                LoggerUtil.RecursivelyLogException(logger, e);

                Environment.Exit(-1);
            }

            WebServiceUtil.Default.AuthTokenRejected += () =>
            {
                systemServices.EnsureGuiRunning();
                ipcServer.NotifyAuthenticationStatus(CloudVeil.IPC.Messages.AuthenticationAction.Required);                
            };

            try
            {
                // Start subsystems
                policyConfiguration.OnConfigurationLoaded += OnConfigLoaded_LoadSelfModeratedSites;

                relaxedPolicy = new RelaxedPolicy(ipcServer, policyConfiguration);

                dnsEnforcement = new DnsEnforcement(policyConfiguration, logger);

                dnsEnforcement.OnCaptivePortalMode += (isCaptivePortal, isActive) =>
                {
                    ipcServer.SendCaptivePortalState(isCaptivePortal, isActive);
                };

                accountability = new Accountability();

                timeDetection = new TimeDetection(SystemClock.Instance);
                timeDetection.ZoneTamperingDetected += OnZoneTampering;

                timeRestrictionsTimer = new Timer(timeRestrictionsCheck, null, 0, 1000);
                retrieveTokenTimer = new Timer(onRetrieveTokenTimeout, null, Timeout.Infinite, Timeout.Infinite);

                siteFiltering = new SiteFiltering(ipcServer, timeDetection, PolicyConfiguration, certificateExemptions);
                siteFiltering.RequestBlocked += OnRequestBlocked;

                policyConfiguration.OnConfigurationLoaded += configureThreshold;
                policyConfiguration.OnConfigurationLoaded += updateTimerFrequency;

                ipcServer.AttemptAuthentication = (args) =>
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(args.Username) && !string.IsNullOrWhiteSpace(args.Username))
                        {
                            byte[] unencrypedPwordBytes = null;
                            try
                            {
                                AuthenticationResultObject authResult = AuthenticationResultObject.FailedResult;
                                bool authOverEmail = args.Action == AuthenticationAction.RequestedWithEmail;

                                if (authOverEmail)
                                {
                                    authResult = WebServiceUtil.Default.AuthenticateByEmail(args.Username);
                                } 
                                else
                                {
                                    unencrypedPwordBytes = args.Password.SecureStringBytes();
                                    authResult = WebServiceUtil.Default.AuthenticateByPassword(args.Username, unencrypedPwordBytes);
                                }

                                switch (authResult.AuthenticationResult)
                                {
                                    case AuthenticationResult.Success:
                                        {
                                            if (authOverEmail)
                                            {
                                                retrieveTokenTimer.Change(RETRIEVE_TOKEN_TIMEOUT, RETRIEVE_TOKEN_TIMEOUT);
                                            } 
                                            else
                                            {
                                                WebServiceUtil.Default.UserEmail = args.Username;
                                                onAuthSuccess();
                                            }
                                        }
                                        break;

                                    case AuthenticationResult.Failure:
                                        {
                                            systemServices.EnsureGuiRunning(runInTray: false);
                                            ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Required, null, new AuthenticationResultObject(AuthenticationResult.Failure, authResult.AuthenticationMessage));
                                        }
                                        break;

                                    case AuthenticationResult.ConnectionFailed:
                                        {
                                            ipcServer.NotifyAuthenticationStatus(AuthenticationAction.ErrorNoInternet);
                                        }
                                        break;
                                }
                            }
                            finally
                            {
                                if (unencrypedPwordBytes != null && unencrypedPwordBytes.Length > 0)
                                {
                                    Array.Clear(unencrypedPwordBytes, 0, unencrypedPwordBytes.Length);
                                    unencrypedPwordBytes = null;
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
                    }
                };

                ipcServer.RegisterRequestHandler(IpcCall.Update, updateSystem.OnRequestUpdate);
                ipcServer.RegisterResponseHandler<UpdateDialogResult>(IpcCall.UpdateResult, updateSystem.OnUpdateDialogResult);
                ipcServer.RegisterRequestHandler<BugReportSetting>(IpcCall.BugReportConfirmationValue, (message) =>
                {
                    var res = message.Data;
                    var needTrigger = AppSettings.Default.BugReportSettings == null || AppSettings.Default.BugReportSettings.Allowed != res.Allowed;
                    AppSettings.Default.BugReportSettings = res;
                    if (!needTrigger)
                    {
                        return true;
                    }

                    AppSettings.Default.Save();

                    if (res.Allowed)
                    {
                        StartSentry();
                    }
                    else
                    {
                        StopSentry();
                    }
                    return true;
                });


                ipcServer.RegisterRequestHandler<Boolean>(IpcCall.RandomizePortsValue, (message) =>
                {
                    if (AppSettings.Default.RandomizePorts != message.Data)
                    {
                        AppSettings.Default.RandomizePorts = message.Data;
                        if (AppSettings.Default.RandomizePorts)
                        {
                            AppSettings.Default.ShufflePorts();
                        } 
                        else
                        {
                            AppSettings.Default.SetDefaultPorts();
                        }

                        RestartFiltering();
                        OnPortsChanged?.Invoke();

                        ipcServer.Send<ushort[]>(IpcCall.PortsValue, new ushort[] { AppSettings.Default.ConfigServerPort, AppSettings.Default.HttpPort, AppSettings.Default.HttpsPort });
                        AppSettings.Default.Save();
                        message.SendReply<bool>(ipcServer, IpcCall.RandomizePortsValue, true);                        
                    } 
                    else
                    {
                        message.SendReply<bool>(ipcServer, IpcCall.RandomizePortsValue, true);
                    }

                    return true;
                } );

                ipcServer.RegisterRequestHandler<Boolean>(IpcCall.DumpSystemEventLog, (message) => {
                    string logSource = "System";
                    string query = "*[System/Provider/@Name=\"Service Control Manager\"]";

                    var elQuery = new EventLogQuery(logSource, PathType.LogName, query);
                    using (var elReader = new System.Diagnostics.Eventing.Reader.EventLogReader(elQuery))
                    {
                        List<string> eventList = new List<string>();
                        var eventInstance = elReader.ReadEvent();
                        try
                        {
                            while (null != eventInstance)
                            {
                                var description = eventInstance.TimeCreated.ToString() + " " + eventInstance.FormatDescription();
                                eventList.Add(description);
                                if (eventInstance != null)
                                {
                                    eventInstance.Dispose();
                                }
                                eventInstance = elReader.ReadEvent();
                            }
                            File.WriteAllLines(LoggerUtil.LogFolderPath + "\\events.txt", eventList);
                        }

                        finally
                        {
                            if (eventInstance != null)
                            {
                                eventInstance.Dispose();
                            }
                        }
                    }
                    return true;
                });
                
                ipcServer.DeactivationRequested = (args) =>
                {
                    Status = FilterStatus.Synchronizing;

                    try
                    {
                        HttpStatusCode responseCode;
                        bool responseReceived;
                        var response = WebServiceUtil.Default.RequestResource(ServiceResource.DeactivationRequest, out responseCode, out responseReceived);

                        if (!responseReceived)
                        {
                            args.DeactivationCommand = DeactivationCommand.NoResponse;
                        }
                        else
                        {
                            args.DeactivationCommand = responseCode == HttpStatusCode.OK || responseCode == HttpStatusCode.NoContent ? DeactivationCommand.Granted : DeactivationCommand.Denied;
                        }

                        if (args.Granted)
                        {
                            Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                        }
                        else
                        {
                            Status = FilterStatus.Running;
                        }
                    }
                    catch (Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
                        Status = FilterStatus.Running;
                    }
                };

                ipcServer.ClientServerStateQueried = (args) =>
                {
                    ipcServer.NotifyStatus(Status);
                };

                ipcServer.RelaxedPolicyRequested = relaxedPolicy.OnRelaxedPolicyRequested;

                ipcServer.RegisterRequestHandler(IpcCall.AddCustomTextTrigger, (message) =>
                {
                    string trigger = message.DataObject as string;
                    if (trigger == null) return false;

                    HttpStatusCode code;
                    bool responseReceived;

                    byte[] responseBytes = WebServiceUtil.Default.RequestResource(ServiceResource.AddSelfModerationEntry, out code, out responseReceived, new WebServiceUtil.ResourceOptions()
                    {
                        Parameters = new Dictionary<string, object>()
                        {
                            { "url", trigger },
                            { "list_type", "triggerBlacklist" }
                        }
                    });

                    if(responseReceived && code == HttpStatusCode.NoContent)
                    {
                        logger.Info("Added custom text trigger {0}", trigger);

                        if(policyConfiguration?.Configuration != null)
                        {
                            policyConfiguration.Configuration.CustomTriggerBlacklist.Add(trigger);
                            policyConfiguration.LoadLists();

                            message.SendReply(ipcServer, IpcCall.AddCustomTextTrigger, policyConfiguration.Configuration.CustomTriggerBlacklist);
                        }
                    }
                    else
                    {
                        logger.Error("Failed to add custom text trigger site");
                    }

                    return true;
                });

                ipcServer.RegisterRequestHandler(IpcCall.AddSelfModeratedSite, (message) =>
                {
                    string site = message.DataObject as string;
                    if (site == null)
                        return false;

                    HttpStatusCode code;
                    bool responseReceived;

                    byte[] responseBytes = WebServiceUtil.Default.RequestResource(ServiceResource.AddSelfModerationEntry, out code, out responseReceived, new WebServiceUtil.ResourceOptions()
                    {
                        Parameters = new Dictionary<string, object>()
                        {
                            { "url", site }
                        }
                    });

                    if (responseReceived && code == HttpStatusCode.NoContent)
                    {
                        logger.Info("Added self moderation site {0}", site);

                        if (policyConfiguration?.Configuration != null)
                        {
                            policyConfiguration.Configuration.SelfModeration.Add(site);
                            policyConfiguration.LoadLists();

                            message.SendReply(ipcServer, IpcCall.AddSelfModeratedSite, policyConfiguration.Configuration.SelfModeration);
                        }
                    }
                    else
                    {
                        logger.Error("Failed to add self-moderation site");
                    }

                    return true;
                });

                ipcServer.ClientRequestsBlockActionReview += (NotifyBlockActionMessage blockActionMsg) =>
                {
                    var curAuthToken = WebServiceUtil.Default.AuthToken;

                    if (curAuthToken != null && curAuthToken.Length > 0)
                    {
                        string deviceName = string.Empty;

                        try
                        {
                            deviceName = Environment.MachineName;
                        }
                        catch
                        {
                            deviceName = "Unknown";
                        }

                        try
                        {
                            var reportPath = WebServiceUtil.Default.ServiceProviderUnblockRequestPath;
                            reportPath = string.Format(
                                @"{0}?category_name={1}&user_id={2}&device_name={3}&blocked_request={4}",
                                reportPath,
                                Uri.EscapeDataString(blockActionMsg.Category),
                                Uri.EscapeDataString(curAuthToken),
                                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(deviceName)),
                                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(blockActionMsg.Resource.ToString()))
                                );

                            PlatformTypes.New<ISystemServices>().OpenUrlInSystemBrowser(new Uri(reportPath));
                        }
                        catch (Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(logger, e);
                        }
                    }
                };

                ipcServer.ClientConnected = () =>
                {
                    try
                    {
                        ConnectedClients++;

                        var cfg = policyConfiguration.Configuration;

                        if (cfg != null)
                            ipcServer.SendConfigurationInfo(cfg);

                        relaxedPolicy.SendRelaxedPolicyInfo();

                        ipcServer.NotifyStatus(Status);

                        dnsEnforcement.Trigger();

                        if (ipcServer.WaitingForAuth)
                        {
                            ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Required);
                        }
                        else
                        {
                            ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Authenticated, WebServiceUtil.Default.UserEmail);

                            string fingerprint = FingerprintService.Default.Value;
                            logger.Info("I am sending a fingerprint value of {0} to client.", fingerprint);

                            ipcServer.Send<UpdateCheckInfo>(IpcCall.CheckForUpdates, new UpdateCheckInfo(AppSettings.Default.LastUpdateCheck, AppSettings.Default.UpdateCheckResult));
                            ipcServer.Send<ConfigCheckInfo>(IpcCall.SynchronizeSettings, new ConfigCheckInfo(AppSettings.Default.LastSettingsCheck, AppSettings.Default.ConfigUpdateResult));
                            ipcServer.Send<BugReportSetting>(IpcCall.BugReportConfirmationValue, AppSettings.Default.BugReportSettings);
                            ipcServer.Send<string>(IpcCall.ActivationIdentifier, fingerprint);
                            ipcServer.Send<ushort[]>(IpcCall.PortsValue, new ushort[] { AppSettings.Default.ConfigServerPort, AppSettings.Default.HttpPort, AppSettings.Default.HttpsPort });
                            ipcServer.Send<bool>(IpcCall.RandomizePortsValue, AppSettings.Default.RandomizePorts);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Warn("Error occurred while trying to connect to IPC server.");
                        LoggerUtil.RecursivelyLogException(logger, ex);
                    }
                };

                ipcServer.ClientDisconnected = () =>
                {
                    ConnectedClients--;

                    // All GUI clients are gone and no one logged in. Shut it down.
                    if (ConnectedClients <= 0 && ipcServer.WaitingForAuth)
                    {
                        Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                    }
                };

                ipcServer.RegisterRequestHandler(IpcCall.CheckForUpdates, (msg) =>
                {
                    var checkResult = CheckForApplicationUpdate(true);

                    // Code smell. It's a little unclear who updates LastUpdateCheck. We should really be accessing a different property, or make it more explicit.
                    msg.SendReply<UpdateCheckInfo>(ipcServer, IpcCall.CheckForUpdates, new UpdateCheckInfo(AppSettings.Default.LastUpdateCheck, checkResult));

                    return true;
                });

                ipcServer.RegisterRequestHandler(IpcCall.SynchronizeSettings, (msg) =>
                {
                    dnsEnforcement.InvalidateDnsResult();
                    dnsEnforcement.Trigger();

                    var result = this.UpdateAndWriteList(true);

                    // Code smell. It's a little unclear who updates LastSettingsCheck. We should really make the accessing of this property more direct.
                    msg.SendReply<ConfigCheckInfo>(ipcServer, IpcCall.SynchronizeSettings, new ConfigCheckInfo(AppSettings.Default.LastSettingsCheck, result));

                    return true;
                });

                ipcServer.RegisterRequestHandler(IpcCall.CollectComputerInfo, (msg) =>
                {
                    logger.Info("Collecting computer information");

                    ComputerInfo info = systemServices.GetComputerInfo();
                    logger.Info("Collected computer information {0}", info?.DiagnosticsText);

                    msg.SendReply<ComputerInfo>(ipcServer, IpcCall.CollectComputerInfo, info);

                    return true;
                });

                ipcServer.RequestCaptivePortalDetection = (msg) =>
                {
                    dnsEnforcement.Trigger();
                };

                ipcServer.OnDiagnosticsEnable = (msg) =>
                {
                };

                ipcServer.Start();
            }
            catch (Exception ipce)
            {
                // This is a critical error. We cannot recover from this.
                logger.Error("Critical error - Could not start IPC server.");
                LoggerUtil.RecursivelyLogException(logger, ipce);

                Environment.Exit(-1);
            }

            LogTime("Done with OnStartup initialization.");

            // Before we do any network stuff, ensure we have windows firewall access.            
            systemServices.EnsureFirewallAccess();

            LogTime("EnsureWindowsFirewallAccess() is done");
            // Run the background init worker for non-UI related initialization.
            backgroundInitWorker = new BackgroundWorker();
            backgroundInitWorker.DoWork += DoBackgroundInit;
            backgroundInitWorker.RunWorkerCompleted += OnBackgroundInitComplete;

            backgroundInitWorker.RunWorkerAsync();
        }

        private void onAuthSuccess()
        {
            Status = FilterStatus.Running;
            ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Authenticated, WebServiceUtil.Default.UserEmail);

            // Probe server for updates now.
            updateSystem.ProbeMasterForApplicationUpdates(false);
            OnUpdateTimerElapsed(null);
        }

         
        private void timeRestrictionsCheck(object state)
        {
            bool? areTimeRestrictionsActive = false;
            try
            {
                try
                {
                    if (policyConfiguration == null || policyConfiguration.Configuration == null)
                    {
                        timeRestrictionsTimer.Change(1000, 1000);
                        return;
                    }

                    TimeRestrictionModel currentDay = policyConfiguration?.TimeRestrictions?[(int)DateTime.Now.DayOfWeek];

                    if (currentDay == null)
                    {
                        timeRestrictionsTimer.Change(30000, 30000);
                        return;
                    }

                    ZonedDateTime currentTime = timeDetection.GetRealTime();

                    if (timeDetection.IsDateTimeAllowed(timeDetection.GetRealTime(), currentDay))
                    {
                        areTimeRestrictionsActive = false;
                    }
                    else
                    {
                        areTimeRestrictionsActive = true;
                    }
                }
                finally
                {
                    if (!(policyConfiguration?.TimeRestrictions?.Any(t => t?.RestrictionsEnabled ?? false) ?? false))
                    {
                        areTimeRestrictionsActive = null;
                    }

                    ipcServer.Send<bool?>(IpcCall.TimeRestrictionsEnabled, areTimeRestrictionsActive);
                }
            }
            catch(Exception ex)
            {
                logger.Error("timeRestrictionsCheck error occurred.");
                LoggerUtil.RecursivelyLogException(logger, ex);
            }
        }

        private void OnZoneTampering(object sender, ZoneTamperingEventArgs e)
        {
            ZonedDateTime currentTime = timeDetection.GetRealTime();

            var date = currentTime.ToDateTimeOffset();

            if (policyConfiguration.AreAnyTimeRestrictionsEnabled)
            {
                accountability.NotifyTimeZoneChanged(e.OldZone, e.NewZone);
            }
        }

        private void OnConfigLoaded_LoadSelfModeratedSites(object sender, EventArgs e)
        {
            ipcServer.SendConfigurationInfo(policyConfiguration.Configuration);
        }

        private Assembly CurrentDomain_TypeResolve(object sender, ResolveEventArgs args)
        {
            logger.Error($"Type resolution failed. Type name: {args.Name}, loading assembly: {args.RequestingAssembly.FullName}");

            return null;
        }

        private void updateTimerFrequency(object sender, EventArgs e)
        {
            if (policyConfiguration.Configuration != null)
            {
                // Put the new update frequence into effect.
                logger.Info($"updateTimerFrequency Setting update timer to {policyConfiguration.Configuration.UpdateFrequency}"); ;
                this.updateCheckTimer?.Change(policyConfiguration.Configuration.UpdateFrequency, Timeout.InfiniteTimeSpan);
            }
        }

        #region Configuration event functions
        private void configureThreshold(object sender, EventArgs e)
        {
            if (policyConfiguration.Configuration != null && policyConfiguration.Configuration.UseThreshold)
            {
                InitThresholdData();
            }
        }

        #endregion

        private void OnAppSessionEnding(object sender, EventArgs e)
        {
            logger.Info("Session ending.");

            // THIS MUST BE DONE HERE ALWAYS, otherwise, we get BSOD.
            var antitampering = PlatformTypes.New<IAntitampering>();
            antitampering.DisableProcessProtection();

            exiting = true;

            Environment.Exit((int)ExitCodes.ShutdownWithSafeguards);
        }

        /// <summary>
        /// Called only in circumstances where the application config requires use of the block
        /// action threshold tracking functionality.
        /// </summary>
        private void InitThresholdData()
        {
            // If exists, stop it first.
            if (thresholdCountTimer != null)
            {
                thresholdCountTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }

            // Create the threshold count timer and start it with the configured timespan.
            var cfg = policyConfiguration.Configuration;
            thresholdCountTimer = new Timer(OnThresholdTriggerPeriodElapsed, null, cfg != null ? cfg.ThresholdTriggerPeriod : TimeSpan.FromMinutes(1), Timeout.InfiniteTimeSpan);

            // Create the enforcement timer, but don't start it.
            thresholdEnforcementTimer = new Timer(OnThresholdTimeoutPeriodElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Sets up the filtering engine, calls establish trust with firefox, sets up callbacks for
        /// classification and firewall checks, but does not start the engine.
        /// </summary>
        private async void InitEngine()
        {
            LogTime("Starting InitEngine()");

            LogTime("Loading filtering engine.");

            // Init the engine with our callbacks, the path to the ca-bundle, let it pick whatever
            // ports it wants for listening, and give it our total processor count on this machine as
            // a hint for how many threads to use.
            //filteringEngine = new WindowsProxyServer(OnAppFirewallCheck, OnHttpMessageBegin, OnHttpMessageEnd, OnBadCertificate);

            // TODO: Code smell. Do we instantiate types with special functions, or do we use PlatformTypes.New<T>() ?
            filteringEngine = systemServices.StartProxyServer(new ProxyConfiguration()
            {
                AuthorityName = "CloudVeil for Windows",
                BeforeRequest = siteFiltering.OnBeforeRequest,
                BeforeResponse = siteFiltering.OnBeforeResponse,
                Blacklisted = siteFiltering.OnBlacklist,
                Whitelisted = siteFiltering.OnWhitelist
            });

            // Setup general info, warning and error events.

            // Start filtering, always.
            if (filteringEngine != null && !filteringEngine.IsRunning)
            {
                filterEngineStartupBgWorker = new BackgroundWorker();
                filterEngineStartupBgWorker.DoWork += ((object sender, DoWorkEventArgs e) =>
                {
                    StartFiltering();
                });

                filterEngineStartupBgWorker.RunWorkerAsync();
            }

            // Now establish trust with FireFox. XXX TODO - This can actually be done elsewhere. We
            // used to have to do this after the engine started up to wait for it to write the CA to
            // disk and then use certutil to install it in FF. However, thanks to FireFox giving the
            // option to trust the local certificate store, we don't have to do that anymore.
            try
            {
                trustManager.EstablishTrust();
            }
            catch (Exception ffe)
            {
                LoggerUtil.RecursivelyLogException(logger, ffe);
            }

            LogTime("Trust established with user apps.");
        }

#if WITH_NLP
        /// <summary>
        /// Used to strip multiple whitespace. 
        /// </summary>
        private Regex whitespaceRegex;

        /// <summary>
        /// Loads the given NLP model and list of categories from within the model that we'll
        /// consider enabled. That is to say, any classification result that yeilds a category found
        /// in the supplied list of enabled categories found within the loaded model will trigger a
        /// block action.
        /// </summary>
        /// <param name="nlpModelBytes">
        /// The bytes from a loaded NLP classification model. 
        /// </param>
        /// <param name="nlpConfig">
        /// A model file describing data about the model, such as a list of categories that, should
        /// they be returned by the classifer, should trigger a block action.
        /// </param>
        /// <remarks>
        /// Note that this must be called AFTER we have already initialized the filtering engine,
        /// because we make calls to enable new categories within the engine.
        /// </remarks>
        private void LoadNlpModel(byte[] nlpModelBytes, NLPConfigurationModel nlpConfig)
        {
            try
            {
                doccatSlimLock.EnterWriteLock();

                var selectedCategoriesHashset = new HashSet<string>(nlpConfig.SelectedCategoryNames, StringComparer.OrdinalIgnoreCase);

                var mappedAllCategorySet = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // Init our regexes
                whitespaceRegex = new Regex(@"\s+", RegexOptions.ECMAScript | RegexOptions.Compiled);

                // Init Document classifier.
                var doccatModel = new DoccatModel(new java.io.ByteArrayInputStream(nlpModelBytes));
                var classifier = new DocumentCategorizerME(doccatModel);

                // Get the number of categories and iterate over all categories in the model.
                var numCategories = classifier.getNumberOfCategories();

                for(int i = 0; i < numCategories; ++i)
                {
                    var modelCategory = classifier.getCategory(i);

                    // Make the category name unique by prepending the relative path the NLP model
                    // file. This will ensure that categories with the same name across multiple NLP
                    // models will be insulated against collision.
                    var relativeNlpPath = nlpConfig.RelativeModelPath.Substring(0, nlpConfig.RelativeModelPath.LastIndexOfAny(new[] { '/', '\\' }) + 1) + Path.GetFileNameWithoutExtension(nlpConfig.RelativeModelPath) + "/";
                    var mappedModelCategory = relativeNlpPath + modelCategory;

                    mappedAllCategorySet.Add(modelCategory, mappedModelCategory);

                    if(selectedCategoriesHashset.Contains(modelCategory))
                    {
                        logger.Info("Setting up NLP classification category: {0}", modelCategory);

                        MappedFilterListCategoryModel existingCategory = null;
                        if(TryFetchOrCreateCategoryMap(mappedModelCategory, out existingCategory))
                        {
                            categoryIndex.SetIsCategoryEnabled(existingCategory.CategoryId, true);
                        }
                        else
                        {
                            logger.Error("Failed to get category map for NLP model.");
                        }
                    }
                }

                // Push this classifier to our list of classifiers.
                documentClassifiers.Add(new CategoryMappedDocumentCategorizerModel(classifier, mappedAllCategorySet));
            }
            finally
            {
                doccatSlimLock.ExitWriteLock();
            }
        }
#endif

        /// <summary>
        /// Runs initialization off the UI thread. 
        /// </summary>
        /// <param name="sender">
        /// Event origin. 
        /// </param>
        /// <param name="e">
        /// Event args. 
        /// </param>
        private void DoBackgroundInit(object sender, DoWorkEventArgs e)
        {
            LogTime("Starting DoBackgroundInit()");

            // Init the Engine in the background.
            try
            {
                Task.Run(() => InitEngine());
            }
            catch (Exception ie)
            {
                LoggerUtil.RecursivelyLogException(logger, ie);
            }

            // Run our extension method if it exists.
            try
            {
                extensionDelegate?.Invoke(this);
            }
            catch(Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
            }


            // Force start our cascade of protective processes.
            try
            {
                if (!isTestRun)
                {
                    systemServices.RunProtectiveServices();
                }
            }
            catch (Exception se)
            {
                LoggerUtil.RecursivelyLogException(logger, se);
            }

            // Init update timer.
            updateCheckTimer = new Timer(OnUpdateTimerElapsed, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);

            // Run log cleanup and schedule for next run.
            OnCleanupLogsElapsed(null);

            // Set up our network availability checks so we can run captive portal detection on a changed network.
            NetworkChange.NetworkAddressChanged += dnsEnforcement.OnNetworkChange;

            // Run on startup so we can get the network state right away.
            dnsEnforcement.Trigger();
        }

        /// <summary>
        /// Called when the application is about to exit. 
        /// </summary>
        /// <param name="sender">
        /// Event origin. 
        /// </param>
        /// <param name="e">
        /// Event args. 
        /// </param>
        private void OnApplicationExiting(object sender, EventArgs e)
        {
            logger.Info("Filter service provider process exiting.");

            try
            {
                // Unhook first.
                AppDomain.CurrentDomain.ProcessExit -= OnApplicationExiting;
            }
            catch (Exception err)
            {
                LoggerUtil.RecursivelyLogException(logger, err);
            }

            try
            {
                if (Environment.ExitCode == (int)ExitCodes.ShutdownWithoutSafeguards)
                {
                    logger.Info("Filter service provider process shutting down without safeguards.");

                    DoCleanShutdown(false);
                }
                else
                {
                    logger.Info("Filter service provider process shutting down with safeguards.");

                    // Unless explicitly told not to, always use safeguards.
                    DoCleanShutdown(true);
                }
            }
            catch (Exception err)
            {
                LoggerUtil.RecursivelyLogException(logger, err);
            }
        }

        /// <summary>
        /// Called when the background initialization function has returned. 
        /// </summary>
        /// <param name="sender">
        /// Event origin. 
        /// </param>
        /// <param name="e">
        /// Event args. 
        /// </param>
        private void OnBackgroundInitComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            // Must ensure we're not blocking internet now that we're running.
            systemServices.EnableInternet();

            if (e.Cancelled || e.Error != null)
            {
                logger.Error("Error during initialization.");
                if (e.Error != null && logger != null)
                {
                    LoggerUtil.RecursivelyLogException(logger, e.Error);
                }

                Environment.Exit((int)ExitCodes.ShutdownInitializationError);
                return;
            }

            OnUpdateTimerElapsed(null);

            Status = FilterStatus.Running;

            systemServices.EnsureGuiRunning(runInTray: true);
        }

        #region EngineCallbacks

        /// <summary>
        /// Called whenever a block action occurs. 
        /// </summary>
        /// <param name="category">
        /// The ID of the category that the blocked content was deemed to belong to. 
        /// </param>
        /// <param name="cause">
        /// The type of classification that led to the block action. 
        /// </param>
        /// <param name="requestUri">
        /// The URI of the request that was blocked or the request which generated the blocked response. 
        /// </param>
        /// <param name="matchingRule">
        /// The raw rule that caused the block action. May not be applicable for all block actions.
        /// Default is empty string.
        /// </param>
        private void OnRequestBlocked(short category, BlockType cause, Uri requestUri, string matchingRule = "")
        {
            bool internetShutOff = false;

            var cfg = policyConfiguration.Configuration;

            if (cfg != null && cfg.UseThreshold)
            {
                var currentTicks = Interlocked.Increment(ref thresholdTicks);

                if (currentTicks >= cfg.ThresholdLimit)
                {
                    internetShutOff = true;

                    try
                    {
                        logger.Warn("Block action threshold met or exceeded. Disabling internet.");
                        systemServices.DisableInternet();
                    }
                    catch (Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
                    }

                    this.thresholdEnforcementTimer.Change(cfg.ThresholdTimeoutPeriod, Timeout.InfiniteTimeSpan);
                }
            }

            string categoryNameString = "Unknown";
            var mappedCategory = policyConfiguration.GeneratedCategoriesMap.Values.Where(xx => xx.CategoryId == category).FirstOrDefault();

            if (mappedCategory != null)
            {
                categoryNameString = mappedCategory.CategoryName;
            }

            ipcServer.NotifyBlockAction(cause, requestUri, categoryNameString, DateTime.Now, matchingRule);
            accountability.AddBlockAction(cause, requestUri, categoryNameString, matchingRule);

            if (internetShutOff)
            {
                var restoreDate = DateTime.Now.AddTicks(cfg != null ? cfg.ThresholdTimeoutPeriod.Ticks : TimeSpan.FromMinutes(1).Ticks);

                var cooldownPeriod = (restoreDate - DateTime.Now);

                ipcServer.NotifyCooldownEnforced(cooldownPeriod);
            }

            logger.Info("Request {0} blocked by rule {1} in category {2}.", requestUri.ToString(), matchingRule, categoryNameString);
        }

        /// <summary>
        /// Called whenever the engine reports that elements were removed from the payload of a
        /// response to the given request.
        /// </summary>
        /// <param name="numElementsRemoved">
        /// The number of elements removed. 
        /// </param>
        /// <param name="fullRequest">
        /// The request who's response payload has had the elements removed. 
        /// </param>
        private void OnElementsBlocked(uint numElementsRemoved, string fullRequest)
        {
            Debug.WriteLine("Elements blocked.");
        }

        /// <summary>
        /// A little helper function for finding a path in a whitelist/blacklist.
        /// </summary>
        /// <param name="list"></param>
        /// <param name="appAbsolutePath"></param>
        /// <param name="appName"></param>
        /// <returns></returns>
        private bool isAppInList(HashSet<string> list, string appAbsolutePath, string appName)
        {
            if (list.Contains(appName))
            {
                // Whitelist is in effect and this app is whitelisted. So, don't force it through.
                return true;
            }

            // Support for whitelisted apps like Android Studio\bin\jre\java.exe
            foreach (string app in list)
            {
                if (app.Contains(Path.DirectorySeparatorChar) && appAbsolutePath.EndsWith(app))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion EngineCallbacks

        /// <summary>
        /// Called by the threshold trigger timer whenever it's set time has passed. Here we'll reset
        /// the current count of block actions we're tracking.
        /// </summary>
        /// <param name="state">
        /// Async state object. Not used. 
        /// </param>
        private void OnThresholdTriggerPeriodElapsed(object state)
        {
            try
            {
                // Reset count to zero.
                Interlocked.Exchange(ref thresholdTicks, 0);

                var cfg = policyConfiguration.Configuration;

                this.thresholdCountTimer.Change(cfg != null ? cfg.ThresholdTriggerPeriod : TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
                // TODO: Tell Sentry about this problem.
            }

        }

        /// <summary>
        /// Called whenever the threshold timeout period has elapsed. Here we'll restore internet access. 
        /// </summary>
        /// <param name="state">
        /// Async state object. Not used. 
        /// </param>
        private void OnThresholdTimeoutPeriodElapsed(object state)
        {
            try
            {
                systemServices.EnableInternet();
            }
            catch (Exception e)
            {
                logger.Warn("Error when trying to reinstate internet on threshold timeout period elapsed.");
                LoggerUtil.RecursivelyLogException(logger, e);
            }

            Status = FilterStatus.Running;

            // Disable the timer before we leave.
            this.thresholdEnforcementTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public UpdateCheckResult CheckForApplicationUpdate(bool isCheckButton)
        {
            logger.Info("Checking for application updates.");
            return updateSystem.ProbeMasterForApplicationUpdates(isCheckButton);
        }

        public ConfigUpdateResult UpdateAndWriteList(bool isSyncButton)
        {
            LogTime("UpdateAndWriteList");
            if (!NetworkStatus.Default.HasConnection)
            {
                NetworkStatus.Default.ConnectionStateChanged += Default_ConnectionStateChanged;
                return ConfigUpdateResult.NoInternet;
            }

            ConfigUpdateResult result = ConfigUpdateResult.ErrorOccurred;

            try
            {
                logger.Info("Checking for filter list updates.");

                updateRwLock.EnterWriteLock();

                bool? configurationDownloaded = policyConfiguration.DownloadConfiguration();

                if (configurationDownloaded == null)
                {
                    result = ConfigUpdateResult.NoInternet;
                }
                else if (configurationDownloaded == true)
                {
                    result = ConfigUpdateResult.Updated;
                }
                else
                {
                    result = ConfigUpdateResult.UpToDate;
                }

                bool doLoadLists = !AdBlockMatcherApi.AreListsLoaded();

                if (policyConfiguration.Configuration == null || configurationDownloaded == true || (configurationDownloaded == null && policyConfiguration.Configuration == null))
                {
                    // Got new data. Gotta reload.
                    bool configLoaded = policyConfiguration.LoadConfiguration();
                    doLoadLists = true;

                    result = ConfigUpdateResult.Updated;

                    // Enforce DNS if present.
                    dnsEnforcement.Trigger();
                }

                bool? listsDownloaded = policyConfiguration.DownloadLists();

                doLoadLists = doLoadLists || listsDownloaded == true || (listsDownloaded == null && !AdBlockMatcherApi.AreListsLoaded());

                if (doLoadLists)
                {
                    policyConfiguration.LoadLists();
                }
                else if (listsDownloaded == null && policyConfiguration.Configuration == null)
                {
                    logger.Error("Was not able to download rulesets due to configuration being null");
                }

            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(logger, e);
            }
            finally
            {
                try
                {
                    // We don't handle all cases in the switch, because we don't want to set LastSettingsCheck for error states.
                    switch (result)
                    {
                        case ConfigUpdateResult.Updated:
                        case ConfigUpdateResult.UpToDate:
                            AppSettings.Default.LastSettingsCheck = DateTime.Now;
                            break;
                    }

                    AppSettings.Default.ConfigUpdateResult = result;
                    AppSettings.Default.Save();

                    ipcServer.Send<ConfigCheckInfo>(IpcCall.SynchronizeSettings, new ConfigCheckInfo(DateTime.Now, result));

                    // Enable the timer again.
                    if (!NetworkStatus.Default.HasConnection)
                    {
                        NetworkStatus.Default.ConnectionStateChanged += Default_ConnectionStateChanged;
                    }
                    else
                    {
                        var cfg = policyConfiguration.Configuration;
                        if (cfg != null)
                        {
                            logger.Info($"UpdateAndWriteList Setting update timer to {policyConfiguration.Configuration.UpdateFrequency}"); ;
                            this.updateCheckTimer.Change(cfg.UpdateFrequency, Timeout.InfiniteTimeSpan);
                        }
                        else
                        {
                            logger.Info($"Setting update timer to fallback"); ;
                            this.updateCheckTimer.Change(TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
                        }
                    }

                }
                catch (Exception ex)
                {
                    logger.Error("Finally block exception");
                    LoggerUtil.RecursivelyLogException(logger, ex);
                }

                updateRwLock.ExitWriteLock();
            }

            return result;
        }

        private void Default_ConnectionStateChanged()
        {
            if (NetworkStatus.Default.HasConnection)
            {
                OnUpdateTimerElapsed(null);
                NetworkStatus.Default.ConnectionStateChanged -= Default_ConnectionStateChanged;
            }
        }

        /// <summary>
        /// Called every X minutes by the update timer. We check for new lists, and hot-swap the
        /// rules if we have found new ones. We also check for program updates.
        /// </summary>
        /// <param name="state">
        /// This is always null. Ignore it. 
        /// </param>
        private void OnUpdateTimerElapsed(object state)
        {
            logger.Info("Running OnUpdateTimerElapsed");

            if (ipcServer != null && ipcServer.WaitingForAuth)
            {
                return;
            }

            this.UpdateAndWriteList(false);
            this.CheckForApplicationUpdate(false);

            this.CleanupLogs();

            if (lastUsernamePrintTime.Date < DateTime.Now.Date)
            {
                lastUsernamePrintTime = DateTime.Now;
                logger.Info($"Currently logged in user is {WebServiceUtil.Default.UserEmail}");
            }
        }

        public const int LogCleanupIntervalInHours = 12;
        public const int MaxLogAgeInDays = 7;

        private void OnCleanupLogsElapsed(object state)
        {
            this.CleanupLogs();

            if (cleanupLogsTimer == null)
            {
                cleanupLogsTimer = new Timer(OnCleanupLogsElapsed, null, TimeSpan.FromHours(LogCleanupIntervalInHours), Timeout.InfiniteTimeSpan);
            }
            else
            {
                cleanupLogsTimer.Change(TimeSpan.FromHours(LogCleanupIntervalInHours), Timeout.InfiniteTimeSpan);
            }
        }

        Stopwatch logTimeStopwatch = null;
        /// <summary>
        /// Logs the amount of time that has passed since the last time this function was called.
        /// </summary>
        /// <param name="message"></param>
        private void LogTime(string message)
        {
            string timeInfo = null;

            if (logTimeStopwatch == null)
            {
                logTimeStopwatch = Stopwatch.StartNew();
                timeInfo = "Initialized:";
            }
            else
            {
                long ms = logTimeStopwatch.ElapsedMilliseconds;
                timeInfo = string.Format("{0}ms:", ms);

                logTimeStopwatch.Restart();
            }

            logger.Info("TIME {0} {1}", timeInfo, message);
        }

        private DateTime? lastCleanupLogDate = null;

        private void CleanupLogs()
        {
            try
            {
                IPathProvider paths = PlatformTypes.New<IPathProvider>();

                DateTime currentDate = DateTime.Now.Date;
                if(lastCleanupLogDate == null || currentDate != lastCleanupLogDate.Value)
                {
                    GoProxy.Instance.SetProxyLogFile(paths.GetPath("logs", $"proxy-{currentDate.ToString("MM-dd-yyyy")}.log"));
                }

                string directoryPath = paths.GetPath("logs");

                if (Directory.Exists(directoryPath))
                {
                    string[] files = Directory.GetFiles(directoryPath);
                    foreach (string filePath in files)
                    {
                        FileInfo info = new FileInfo(filePath);

                        DateTime expiryDate = info.LastWriteTime.AddDays(MaxLogAgeInDays);
                        if (expiryDate < DateTime.Now)
                        {
                            info.Delete();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
                // TODO: Tell sentry about this.
            }
        }

        /// <summary>
        /// Starts the filtering engine. 
        /// </summary>
        private void StartFiltering()
        {
            logger.Info(nameof(StartFiltering));
            // Let's make sure we've pulled our internet block.
            try
            {
                systemServices.EnableInternet();
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
            }

            try
            {
                if (filteringEngine != null && !filteringEngine.IsRunning)
                {
                    logger.Info("Start engine.");

                    // Start the engine right away, to ensure the atomic bool IsRunning is set.
                    filteringEngine.Start();
                }
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(logger, e);
            }
        }

        /// <summary>
        /// Occurs when the filtering engine is stopped.
        /// </summary>
        public event EventHandler OnStopFiltering;

        /// <summary>
        /// Stops the filtering engine, shuts it down. 
        /// </summary>
        private async void StopFiltering()
        {
            logger.Info("Stop filtering");
            if (filteringEngine != null && filteringEngine.IsRunning)
            {
                filteringEngine.Stop();
            }
            
           /* Disable DNS Controlling
            * bool isDnsUp = await dnsEnforcement.IsDnsUp();
            if (isDnsUp) {
               dnsEnforcement.SetDnsToDhcp(false);
            }*/

            try
            {
                OnStopFiltering?.Invoke(null, null);
            }
            catch (Exception e)
            {
                logger.Error("Error occurred in OnStopFiltering event");
                LoggerUtil.RecursivelyLogException(logger, e);
            }
        }

        /// <summary>
        /// Called whenever the app is shut down with an authorized key, or when the system is
        /// shutting down, or when the user is logging off.
        /// </summary>
        /// <param name="installSafeguards">
        /// Indicates whether or not safeguards should be put in place when we exit the application
        /// here. Safeguards means that we're going to do all that we can to ensure that our function
        /// is not bypassed, and that we're going to be forced to run again.
        /// </param>
        private void DoCleanShutdown(bool installSafeguards)
        {
            // No matter what, ensure that all GUI instances for all users are
            // immediately shut down, because we, the service, are shutting down.
            systemServices.KillAllGuis();

            lock (cleanShutdownLock)
            {
                if (!cleanShutdownComplete)
                {
                    ipcServer.Dispose();

                    try
                    {
                        // Pull our critical status.
                        PlatformTypes.New<IAntitampering>().DisableProcessProtection();
                    }
                    catch (Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
                    }

                    try
                    {
                        // Shut down engine.
                        StopFiltering();
                    }
                    catch (Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
                    }

                    if (installSafeguards)
                    {
                        try
                        {
                            // Ensure we're automatically running at startup.
                            var scProcNfo = new ProcessStartInfo("sc.exe");
                            scProcNfo.UseShellExecute = false;
                            scProcNfo.WindowStyle = ProcessWindowStyle.Hidden;
                            scProcNfo.Arguments = "config \"FilterServiceProvider\" start= auto";
                            Process.Start(scProcNfo).WaitForExit();
                        }
                        catch (Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(logger, e);
                        }

                        try
                        {
                            var cfg = policyConfiguration.Configuration;
                            if (cfg != null && cfg.BlockInternet)
                            {
                                // While we're here, let's disable the internet so that the user
                                // can't browse the web without us. Only do this of course if configured.
                                try
                                {
                                    systemServices.DisableInternet();
                                }
                                catch { }
                            }
                        }
                        catch (Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(logger, e);
                        }
                    }
                    else
                    {
                        // Means that our user got a granted deactivation request, or installed but
                        // never activated.
                        logger.Info("Shutting down without safeguards.");
                    }

                    // Flag that clean shutdown was completed already.
                    cleanShutdownComplete = true;
                }
            }
        }
    }
}
