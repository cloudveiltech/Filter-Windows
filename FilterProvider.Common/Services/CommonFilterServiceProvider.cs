/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Util;
using Citadel.Core.Windows.Util.Update;
using Citadel.IPC;
using Citadel.IPC.Messages;
using Filter.Platform.Common.Data.Models;
using DistillNET;
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
using Te.Citadel.Util;

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
using LogMessageType = Unosquare.Swan.LogMessageType;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Filter.Platform.Common.IPC.Messages;

namespace FilterProvider.Common.Services
{
    /// <summary>
    /// This is an optional delegate that the common filter services provider can call after its services are initialized.
    /// Use this for platform-specific behaviors. Not intended to replace PlatformTypes.
    /// </summary>
    /// <param name="provider"></param>
    public delegate void ExtensionDelegate(CommonFilterServiceProvider provider);

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
            // We always return false because we don't let anyone tell us that we're going to stop.
            return false;
        }

        public bool Shutdown()
        {
            // Called on a shutdown event.
            Environment.Exit((int)ExitCodes.ShutdownWithSafeguards);
            return true;
        }

        public void OnSessionChanged()
        {
            m_systemServices.EnsureGuiRunning(runInTray: true);
        }

        #endregion Windows Service API

        private FilterStatus Status
        {
            get
            {
                try
                {
                    m_currentStatusLock.EnterReadLock();

                    return m_currentStatus;
                }
                finally
                {
                    m_currentStatusLock.ExitReadLock();
                }
            }

            set
            {
                try
                {
                    m_currentStatusLock.EnterWriteLock();

                    m_currentStatus = value;
                }
                finally
                {
                    m_currentStatusLock.ExitWriteLock();
                }

                m_ipcServer.NotifyStatus(m_currentStatus);
            }
        }

        private int ConnectedClients
        {
            get
            {
                return Interlocked.CompareExchange(ref m_connectedClients, m_connectedClients, 0);
            }

            set
            {
                Interlocked.Exchange(ref m_connectedClients, value);
            }
        }

        /// <summary>
        /// Our current filter status. 
        /// </summary>
        private FilterStatus m_currentStatus = FilterStatus.Synchronizing;

        /// <summary>
        /// Our status lock. 
        /// </summary>
        private ReaderWriterLockSlim m_currentStatusLock = new ReaderWriterLockSlim();

        /// <summary>
        /// The number of IPC clients connected to this server. 
        /// </summary>
        private int m_connectedClients = 0;

        #region FilteringEngineVars

        /// <summary>
        /// Used to strip multiple whitespace. 
        /// </summary>
        private Regex m_whitespaceRegex;

        private IPCServer m_ipcServer;

        public IPCServer IPCServer => m_ipcServer;
        /// <summary>
        /// Used for synchronization whenever our NLP model gets updated while we're already initialized. 
        /// </summary>
        private ReaderWriterLockSlim m_doccatSlimLock = new ReaderWriterLockSlim();

#if WITH_NLP
        private List<CategoryMappedDocumentCategorizerModel> m_documentClassifiers = new List<CategoryMappedDocumentCategorizerModel>();
#endif

        private IProxyServer m_filteringEngine;

        private BackgroundWorker m_filterEngineStartupBgWorker;

        private byte[] m_blockedHtmlPage;
        private byte[] m_badSslHtmlPage;

        private static readonly DateTime s_Epoch = new DateTime(1970, 1, 1);

        private static readonly string s_EpochHttpDateTime = s_Epoch.ToString("r");

        /// <summary>
        /// Applications we never ever want to filter. Right now, this is just OS binaries. 
        /// </summary>
        private static readonly HashSet<string> s_foreverWhitelistedApplications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        #endregion FilteringEngineVars

        private ReaderWriterLockSlim m_filteringRwLock = new ReaderWriterLockSlim();

        private ReaderWriterLockSlim m_updateRwLock = new ReaderWriterLockSlim();

        private UpdateSystem m_updateSystem;

        /// <summary>
        /// Timer used to query for filter list changes every X minutes, as well as application updates. 
        /// </summary>
        private Timer m_updateCheckTimer;

        /// <summary>
        /// Timer used to cleanup logs every 12 hours.
        /// </summary>
        private Timer m_cleanupLogsTimer;

        /// <summary>
        /// Keep track of the last time we printed the username of the current user so we can output it
        /// to the diagnostics log.
        /// </summary>
        private DateTime m_lastUsernamePrintTime = DateTime.MinValue;

        /// <summary>
        /// Since clean shutdown can be called from a couple of different places, we'll use this and
        /// some locks to ensure it's only done once.
        /// </summary>
        private volatile bool m_cleanShutdownComplete = false;

        /// <summary>
        /// Used to ensure clean shutdown once. 
        /// </summary>
        private Object m_cleanShutdownLock = new object();

        /// <summary>
        /// Logger. 
        /// </summary>
        private Logger m_logger;

        /// <summary>
        /// This BackgroundWorker object handles initializing the application off the UI thread.
        /// Allows the splash screen to function.
        /// </summary>
        private BackgroundWorker m_backgroundInitWorker;

        /// <summary>
        /// App function config file. 
        /// </summary>
        IPolicyConfiguration m_policyConfiguration;

        public IPolicyConfiguration PolicyConfiguration
        {
            get
            {
                return m_policyConfiguration;
            }
        }

        /// <summary>
        /// This int stores the number of block actions that have elapsed within the given threshold timespan.
        /// </summary>
        private long m_thresholdTicks;

        /// <summary>
        /// This timer resets the threshold tick count. 
        /// </summary>
        private Timer m_thresholdCountTimer;

        /// <summary>
        /// This timer is used when the threshold has been hit. It is used to set an expiry period
        /// for the internet lockout once the threshold has been hit.
        /// </summary>
        private Timer m_thresholdEnforcementTimer;

        /// <summary>
        /// This timer is used to count down to the expiry time for relaxed policy use. 
        /// </summary>
        private Timer m_relaxedPolicyExpiryTimer;

        /// <summary>
        /// This timer is used to track a 24 hour cooldown period after the exhaustion of all
        /// available relaxed policy uses. Once the timer is expired, it will reset the count to the
        /// config default and then disable itself.
        /// </summary>
        private Timer m_relaxedPolicyResetTimer;

        /// <summary>
        /// Allows us to periodically check status of time restrictions.
        /// </summary>
        private Timer m_timeRestrictionsTimer;

        private DnsEnforcement m_dnsEnforcement;

        private Accountability m_accountability;

        private TimeDetection m_timeDetection;

        private IPlatformTrust m_trustManager;

        private CertificateExemptions m_certificateExemptions = new CertificateExemptions();

        private Server m_controlServer;

        private IPathProvider m_platformPaths;

        private ISystemServices m_systemServices;

        private ExtensionDelegate m_extensionDelegate;

        /// <summary>
        /// Default ctor. 
        /// </summary>
        public CommonFilterServiceProvider(ExtensionDelegate extensionDelegate)
        {
            m_trustManager = PlatformTypes.New<IPlatformTrust>();
            m_platformPaths = PlatformTypes.New<IPathProvider>();
            m_systemServices = PlatformTypes.New<ISystemServices>();
            m_extensionDelegate = extensionDelegate;
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

        private void OnStartup()
        {
            if (File.Exists("debug-filterserviceprovider"))
            {
                Debugger.Launch();
            }

            // We spawn a new thread to initialize all this code so that we can start the service and return control to the Service Control Manager.
            bool consoleOutStatus = false;

            try
            {
                // I have reason to suspect that on some 1803 computers, this statement (or some of this initialization) was hanging, causing an error.
                // on service control manager.
                m_logger = LoggerUtil.GetAppWideLogger();
            }
            catch (Exception ex)
            {
                File.AppendAllText(Path.Combine(m_platformPaths.ApplicationDataFolder, "FatalCrashLog.log"), $"Fatal crash. {ex.ToString()}");
            }

            try
            {
                Console.SetOut(new ConsoleLogWriter());
                Console.SetError(new ConsoleLogWriter("error"));

                consoleOutStatus = true;
            }
            catch (Exception ex)
            {

            }

            string appVerStr = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            appVerStr += " " + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString();
            appVerStr += " " + (Environment.Is64BitProcess ? "x64" : "x86");

            m_logger.Info("CitadelService Version: {0}", appVerStr);

            try
            {
                m_ipcServer = new IPCServer();
                m_policyConfiguration = new DefaultPolicyConfiguration(m_ipcServer, m_logger, m_filteringRwLock);
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ex);
                return;
            }

            if (!consoleOutStatus)
            {
                m_logger.Warn("Failed to link console output to file.");
            }

            // Enforce good/proper protocols
            ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & ~SecurityProtocolType.Ssl3) | (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);

            // Load authtoken and email data from files.
            if (WebServiceUtil.Default.AuthToken == null)
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
                    }
                    catch
                    {

                    }
                } // else let them continue. They'll have to enter their password if this if isn't taken.
            }

            // Hook the shutdown/logoff event.

            // TODO:X_PLAT
            m_systemServices.SessionEnding += OnAppSessionEnding;
            //SystemEvents.SessionEnding += OnAppSessionEnding;

            m_systemServices.OnStartProxy += (sender, e) =>
            {
                try
                {
                    BCCertificateMaker maker = new BCCertificateMaker();

                    AsymmetricCipherKeyPair keyPair = BCCertificateMaker.CreateKeyPair(2048);

                    X509Certificate2 cert = maker.MakeCertificate("localhost", false, m_systemServices.RootCertificate, keyPair, alternateNames: new Asn1Encodable[] {
                        new GeneralName(GeneralName.DnsName, "localhost"),
                        new GeneralName(GeneralName.IPAddress, "127.0.0.1"),
                        new GeneralName(GeneralName.IPAddress, "::1")
                        });

                    m_controlServer = new Server(14299, cert);
                    m_controlServer.RegisterController(typeof(CertificateExemptionsController), (context) => new CertificateExemptionsController(m_certificateExemptions, context));
                    m_controlServer.Start();
                }
                catch(Exception ex)
                {
                    m_logger.Error(ex, "An error occurred while attempting to start the control server.");
                }
            };

            Unosquare.Swan.Terminal.OnLogMessageReceived += Terminal_OnLogMessageReceived;

            // Hook app exiting function. This must be done on this main app thread.
            AppDomain.CurrentDomain.ProcessExit += OnApplicationExiting;

            try
            {
                m_updateSystem = new UpdateSystem(m_policyConfiguration, m_ipcServer, "cv4w1.7");
            }
            catch (Exception e)
            {
                // This is a critical error. We cannot recover from this.
                m_logger.Error("Critical error - Could not create application updater.");
                LoggerUtil.RecursivelyLogException(m_logger, e);

                Environment.Exit(-1);
            }



            WebServiceUtil.Default.AuthTokenRejected += () =>
            {
                m_systemServices.EnsureGuiRunning();
                m_ipcServer.NotifyAuthenticationStatus(Citadel.IPC.Messages.AuthenticationAction.Required);
            };

            try
            {
                m_policyConfiguration.OnConfigurationLoaded += OnConfigLoaded_LoadRelaxedPolicy;
                m_policyConfiguration.OnConfigurationLoaded += OnConfigLoaded_LoadSelfModeratedSites;

                m_dnsEnforcement = new DnsEnforcement(m_policyConfiguration, m_logger);

                m_dnsEnforcement.OnCaptivePortalMode += (isCaptivePortal, isActive) =>
                {
                    m_ipcServer.SendCaptivePortalState(isCaptivePortal, isActive);
                };

                m_dnsEnforcement.OnDnsEnforcementUpdate += (isEnforcementActive) =>
                {

                };

                m_accountability = new Accountability();

                m_timeDetection = new TimeDetection(SystemClock.Instance);
                m_timeDetection.ZoneTamperingDetected += OnZoneTampering;

                m_timeRestrictionsTimer = new Timer(timeRestrictionsCheck, null, 0, 1000);

                m_policyConfiguration.OnConfigurationLoaded += configureThreshold;
                m_policyConfiguration.OnConfigurationLoaded += reportRelaxedPolicy;
                m_policyConfiguration.OnConfigurationLoaded += updateTimerFrequency;

                m_ipcServer.AttemptAuthentication = (args) =>
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(args.Username) && !string.IsNullOrWhiteSpace(args.Username) && args.Password != null && args.Password.Length > 0)
                        {
                            byte[] unencrypedPwordBytes = null;
                            try
                            {
                                unencrypedPwordBytes = args.Password.SecureStringBytes();

                                var authResult = WebServiceUtil.Default.Authenticate(args.Username, unencrypedPwordBytes);

                                switch (authResult.AuthenticationResult)
                                {
                                    case AuthenticationResult.Success:
                                        {
                                            Status = FilterStatus.Running;
                                            m_ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Authenticated);

                                            // Probe server for updates now.
                                            m_updateSystem.ProbeMasterForApplicationUpdates(false);
                                            OnUpdateTimerElapsed(null);
                                        }
                                        break;

                                    case AuthenticationResult.Failure:
                                        {
                                            m_systemServices.EnsureGuiRunning(runInTray: false);
                                            m_ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Required, null, new AuthenticationResultObject(AuthenticationResult.Failure, authResult.AuthenticationMessage));
                                        }
                                        break;

                                    case AuthenticationResult.ConnectionFailed:
                                        {
                                            m_ipcServer.NotifyAuthenticationStatus(AuthenticationAction.ErrorNoInternet);
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
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }
                };

                m_ipcServer.RegisterRequestHandler(IpcCall.Update, m_updateSystem.OnRequestUpdate);
                m_ipcServer.RegisterResponseHandler<UpdateDialogResult>(IpcCall.UpdateResult, m_updateSystem.OnUpdateDialogResult);

                m_ipcServer.DeactivationRequested = (args) =>
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
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                        Status = FilterStatus.Running;
                    }
                };

                m_ipcServer.ClientServerStateQueried = (args) =>
                {
                    m_ipcServer.NotifyStatus(Status);
                };

                m_ipcServer.RelaxedPolicyRequested = (args) =>
                {
                    switch (args.Command)
                    {
                        case RelaxedPolicyCommand.Relinquished:
                            {
                                OnRelinquishRelaxedPolicyRequested();
                            }
                            break;

                        case RelaxedPolicyCommand.Requested:
                            {
                                OnRelaxedPolicyRequested(args);
                            }
                            break;
                    }
                };

                m_ipcServer.RegisterRequestHandler(IpcCall.AddSelfModeratedSite, (message) =>
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
                        m_logger.Info("Added self moderation site {0}", site);

                        if (m_policyConfiguration?.Configuration != null)
                        {
                            m_policyConfiguration.Configuration.SelfModeration.Add(site);
                            m_policyConfiguration.LoadLists();

                            message.SendReply(m_ipcServer, IpcCall.AddSelfModeratedSite, m_policyConfiguration.Configuration.SelfModeration);
                        }
                    }
                    else
                    {
                        m_logger.Error("Failed to add self-moderation site");
                    }

                    return true;
                });

                m_ipcServer.ClientRequestsBlockActionReview += (NotifyBlockActionMessage blockActionMsg) =>
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
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }
                };

                m_ipcServer.ClientConnected = () =>
                {
                    try
                    {
                        ConnectedClients++;

                        var cfg = m_policyConfiguration.Configuration;

                        if (cfg != null)
                            m_ipcServer.SendConfigurationInfo(cfg);

                        if (cfg != null && cfg.BypassesPermitted > 0)
                        {
                            m_ipcServer.NotifyRelaxedPolicyChange(cfg.BypassesPermitted - cfg.BypassesUsed, cfg.BypassDuration, getRelaxedPolicyStatus());
                        }
                        else
                        {
                            m_ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero, getRelaxedPolicyStatus());
                        }

                        m_ipcServer.NotifyStatus(Status);

                        m_dnsEnforcement.Trigger();

                        if (m_ipcServer.WaitingForAuth)
                        {
                            m_ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Required);
                        }
                        else
                        {
                            m_ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Authenticated, WebServiceUtil.Default.UserEmail);

                            m_ipcServer.Send<UpdateCheckInfo>(IpcCall.CheckForUpdates, new UpdateCheckInfo(AppSettings.Default.LastUpdateCheck, AppSettings.Default.UpdateCheckResult));
                            m_ipcServer.Send<ConfigCheckInfo>(IpcCall.SynchronizeSettings, new ConfigCheckInfo(AppSettings.Default.LastSettingsCheck, AppSettings.Default.ConfigUpdateResult));
                        }
                    }
                    catch (Exception ex)
                    {
                        m_logger.Warn("Error occurred while trying to connect to IPC server.");
                        LoggerUtil.RecursivelyLogException(m_logger, ex);
                    }
                };

                m_ipcServer.ClientDisconnected = () =>
                {
                    ConnectedClients--;

                    // All GUI clients are gone and no one logged in. Shut it down.
                    if (ConnectedClients <= 0 && m_ipcServer.WaitingForAuth)
                    {
                        Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                    }
                };

                m_ipcServer.RegisterRequestHandler(IpcCall.CheckForUpdates, (msg) =>
                {
                    var checkResult = CheckForApplicationUpdate(true);

                    // Code smell. It's a little unclear who updates LastUpdateCheck. We should really be accessing a different property, or make it more explicit.
                    msg.SendReply<UpdateCheckInfo>(m_ipcServer, IpcCall.CheckForUpdates, new UpdateCheckInfo(AppSettings.Default.LastUpdateCheck, checkResult));

                    return true;
                });

                m_ipcServer.RegisterRequestHandler(IpcCall.SynchronizeSettings, (msg) =>
                {
                    m_dnsEnforcement.InvalidateDnsResult();
                    m_dnsEnforcement.Trigger();

                    var result = this.UpdateAndWriteList(true);

                    // Code smell. It's a little unclear who updates LastSettingsCheck. We should really make the accessing of this property more direct.
                    msg.SendReply<ConfigCheckInfo>(m_ipcServer, IpcCall.SynchronizeSettings, new ConfigCheckInfo(AppSettings.Default.LastSettingsCheck, result));

                    return true;
                });

                m_ipcServer.RegisterRequestHandler(IpcCall.CollectComputerInfo, (msg) =>
                {
                    m_logger.Info("Collecting computer information");

                    ComputerInfo info = m_systemServices.GetComputerInfo();
                    m_logger.Info("Collected computer information {0}", info?.DiagnosticsText);

                    msg.SendReply<ComputerInfo>(m_ipcServer, IpcCall.CollectComputerInfo, info);

                    return true;
                });

                m_ipcServer.RequestCaptivePortalDetection = (msg) =>
                {
                    m_dnsEnforcement.Trigger();
                };

                m_ipcServer.OnCertificateExemptionGranted = (msg) =>
                {
                    m_certificateExemptions.TrustCertificate(msg.Host, msg.CertificateHash);
                };

                m_ipcServer.OnDiagnosticsEnable = (msg) =>
                {
                    // TODO:X_PLAT
                    //CitadelCore.Diagnostics.Collector.IsDiagnosticsEnabled = msg.EnableDiagnostics;
                };

                // Hooks for CitadelCore diagnostics.

                // TODO:X_PLAT
                /*
                CitadelCore.Diagnostics.Collector.OnSessionReported += (webSession) =>
                {
                    m_logger.Info("OnSessionReported");

                    m_ipcServer.SendDiagnosticsInfo(new DiagnosticsInfoV1()
                    {
                        DiagnosticsType = DiagnosticsType.RequestSession,

                        ClientRequestBody = webSession.ClientRequestBody,
                        ClientRequestHeaders = webSession.ClientRequestHeaders,
                        ClientRequestUri = webSession.ClientRequestUri,

                        ServerRequestBody = webSession.ServerRequestBody,
                        ServerRequestHeaders = webSession.ServerRequestHeaders,
                        ServerRequestUri = webSession.ServerRequestUri,

                        ServerResponseBody = webSession.ServerResponseBody,
                        ServerResponseHeaders = webSession.ServerResponseHeaders,

                        DateStarted = webSession.DateStarted,
                        DateEnded = webSession.DateEnded
                    });
                };*/

                m_ipcServer.Start();
            }
            catch (Exception ipce)
            {
                // This is a critical error. We cannot recover from this.
                m_logger.Error("Critical error - Could not start IPC server.");
                LoggerUtil.RecursivelyLogException(m_logger, ipce);

                Environment.Exit(-1);
            }

            LogTime("Done with OnStartup initialization.");

            // Before we do any network stuff, ensure we have windows firewall access.
            m_systemServices.EnsureFirewallAccess();

            LogTime("EnsureWindowsFirewallAccess() is done");

            // Run the background init worker for non-UI related initialization.
            m_backgroundInitWorker = new BackgroundWorker();
            m_backgroundInitWorker.DoWork += DoBackgroundInit;
            m_backgroundInitWorker.RunWorkerCompleted += OnBackgroundInitComplete;

            m_backgroundInitWorker.RunWorkerAsync();
        }

        // Now why would you code it like this? Because you're lazy.
        private void Terminal_OnLogMessageReceived(object sender, Unosquare.Swan.LogMessageReceivedEventArgs e)
        {
            m_logger.Info($"SWAN: {e.Source}: {e.Message}: {e.Exception?.ToString()}");
        }

        private void timeRestrictionsCheck(object state)
        {
            bool? areTimeRestrictionsActive = false;
            try
            {
                try
                {
                    if (m_policyConfiguration == null || m_policyConfiguration.Configuration == null)
                    {
                        m_timeRestrictionsTimer.Change(1000, 1000);
                        return;
                    }

                    TimeRestrictionModel currentDay = m_policyConfiguration?.TimeRestrictions?[(int)DateTime.Now.DayOfWeek];

                    if (currentDay == null)
                    {
                        m_timeRestrictionsTimer.Change(30000, 30000);
                        return;
                    }

                    ZonedDateTime currentTime = m_timeDetection.GetRealTime();

                    if (m_timeDetection.IsDateTimeAllowed(m_timeDetection.GetRealTime(), currentDay))
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
                    if (!(m_policyConfiguration?.TimeRestrictions?.Any(t => t?.RestrictionsEnabled ?? false) ?? false))
                    {
                        areTimeRestrictionsActive = null;
                    }

                    m_ipcServer.Send<bool?>(IpcCall.TimeRestrictionsEnabled, areTimeRestrictionsActive);
                }
            }
            catch(Exception ex)
            {
                m_logger.Error("timeRestrictionsCheck error occurred.");
                LoggerUtil.RecursivelyLogException(m_logger, ex);
            }
        }

        private void OnZoneTampering(object sender, ZoneTamperingEventArgs e)
        {
            ZonedDateTime currentTime = m_timeDetection.GetRealTime();

            var date = currentTime.ToDateTimeOffset();

            if (m_policyConfiguration.AreAnyTimeRestrictionsEnabled)
            {
                m_accountability.NotifyTimeZoneChanged(e.OldZone, e.NewZone);
            }
        }

        private void OnConfigLoaded_LoadSelfModeratedSites(object sender, EventArgs e)
        {
            m_ipcServer.SendConfigurationInfo(m_policyConfiguration.Configuration);
        }

        private Assembly CurrentDomain_TypeResolve(object sender, ResolveEventArgs args)
        {
            m_logger.Error($"Type resolution failed. Type name: {args.Name}, loading assembly: {args.RequestingAssembly.FullName}");

            return null;
        }

        private void updateTimerFrequency(object sender, EventArgs e)
        {
            if (m_policyConfiguration.Configuration != null)
            {
                // Put the new update frequence into effect.
                this.m_updateCheckTimer.Change(m_policyConfiguration.Configuration.UpdateFrequency, Timeout.InfiniteTimeSpan);
            }
        }

        #region Configuration event functions
        private void reportRelaxedPolicy(object sender, EventArgs e)
        {
            var config = m_policyConfiguration.Configuration;

            // XXX FIXME Update our dashboard view model if there are bypasses
            // configured. Force this up to the UI thread because it's a UI model.
            if (config.BypassesPermitted > 0)
            {
                m_ipcServer.NotifyRelaxedPolicyChange(config.BypassesPermitted, config.BypassDuration, getRelaxedPolicyStatus());
            }
            else
            {
                m_ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero, getRelaxedPolicyStatus());
            }
        }

        private void configureThreshold(object sender, EventArgs e)
        {
            if (m_policyConfiguration.Configuration != null && m_policyConfiguration.Configuration.UseThreshold)
            {
                InitThresholdData();
            }
        }

        #endregion

        private void OnAppSessionEnding(object sender, EventArgs e)
        {
            m_logger.Info("Session ending.");

            // THIS MUST BE DONE HERE ALWAYS, otherwise, we get BSOD.
            var antitampering = PlatformTypes.New<IAntitampering>();
            antitampering.DisableProcessProtection();

            Environment.Exit((int)ExitCodes.ShutdownWithSafeguards);
        }

        /// <summary>
        /// Called only in circumstances where the application config requires use of the block
        /// action threshold tracking functionality.
        /// </summary>
        private void InitThresholdData()
        {
            // If exists, stop it first.
            if (m_thresholdCountTimer != null)
            {
                m_thresholdCountTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }

            // Create the threshold count timer and start it with the configured timespan.
            var cfg = m_policyConfiguration.Configuration;
            m_thresholdCountTimer = new Timer(OnThresholdTriggerPeriodElapsed, null, cfg != null ? cfg.ThresholdTriggerPeriod : TimeSpan.FromMinutes(1), Timeout.InfiniteTimeSpan);

            // Create the enforcement timer, but don't start it.
            m_thresholdEnforcementTimer = new Timer(OnThresholdTimeoutPeriodElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Sets up the filtering engine, calls establish trust with firefox, sets up callbacks for
        /// classification and firewall checks, but does not start the engine.
        /// </summary>
        private async void InitEngine()
        {
            LogTime("Starting InitEngine()");

            // Get our blocked HTML page
            m_blockedHtmlPage = ResourceStreams.Get("FilterProvider.Common.Resources.BlockedPage.html");
            m_badSslHtmlPage = ResourceStreams.Get("FilterProvider.Common.Resources.BadCertPage.html");

            if (m_blockedHtmlPage == null)
            {
                m_logger.Error("Could not load packed HTML block page.");
            }

            if (m_badSslHtmlPage == null)
            {
                m_logger.Error("Could not load packed HTML bad SSL page.");
            }

            LogTime("Loading filtering engine.");

            // Init the engine with our callbacks, the path to the ca-bundle, let it pick whatever
            // ports it wants for listening, and give it our total processor count on this machine as
            // a hint for how many threads to use.
            //m_filteringEngine = new WindowsProxyServer(OnAppFirewallCheck, OnHttpMessageBegin, OnHttpMessageEnd, OnBadCertificate);

            // TODO: Code smell. Do we instantiate types with special functions, or do we use PlatformTypes.New<T>() ?
            m_filteringEngine = m_systemServices.StartProxyServer(new ProxyConfiguration()
            {
                AuthorityName = "CloudVeil for Windows",
                BeforeRequest = OnHttpRequestBegin,
                BeforeResponse = OnBeforeResponse
            });

            // Setup general info, warning and error events.

            // Start filtering, always.
            if (m_filteringEngine != null && !m_filteringEngine.IsRunning)
            {
                m_filterEngineStartupBgWorker = new BackgroundWorker();
                m_filterEngineStartupBgWorker.DoWork += ((object sender, DoWorkEventArgs e) =>
                {
                    StartFiltering();
                });

                m_filterEngineStartupBgWorker.RunWorkerAsync();
            }

            // Now establish trust with FireFox. XXX TODO - This can actually be done elsewhere. We
            // used to have to do this after the engine started up to wait for it to write the CA to
            // disk and then use certutil to install it in FF. However, thanks to FireFox giving the
            // option to trust the local certificate store, we don't have to do that anymore.
            try
            {
                m_trustManager.EstablishTrust();
            }
            catch (Exception ffe)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ffe);
            }

            LogTime("Trust established with user apps.");
        }

#if WITH_NLP
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
                m_doccatSlimLock.EnterWriteLock();

                var selectedCategoriesHashset = new HashSet<string>(nlpConfig.SelectedCategoryNames, StringComparer.OrdinalIgnoreCase);

                var mappedAllCategorySet = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // Init our regexes
                m_whitespaceRegex = new Regex(@"\s+", RegexOptions.ECMAScript | RegexOptions.Compiled);

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
                        m_logger.Info("Setting up NLP classification category: {0}", modelCategory);

                        MappedFilterListCategoryModel existingCategory = null;
                        if(TryFetchOrCreateCategoryMap(mappedModelCategory, out existingCategory))
                        {
                            m_categoryIndex.SetIsCategoryEnabled(existingCategory.CategoryId, true);
                        }
                        else
                        {
                            m_logger.Error("Failed to get category map for NLP model.");
                        }
                    }
                }

                // Push this classifier to our list of classifiers.
                m_documentClassifiers.Add(new CategoryMappedDocumentCategorizerModel(classifier, mappedAllCategorySet));
            }
            finally
            {
                m_doccatSlimLock.ExitWriteLock();
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
                LoggerUtil.RecursivelyLogException(m_logger, ie);
            }

            // Run our extension method if it exists.
            try
            {
                m_extensionDelegate?.Invoke(this);
            }
            catch(Exception ex)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ex);
            }


            // Force start our cascade of protective processes.
            try
            {
                if (!isTestRun)
                {
                    m_systemServices.RunProtectiveServices();
                }
            }
            catch (Exception se)
            {
                LoggerUtil.RecursivelyLogException(m_logger, se);
            }

            // Init update timer.
            m_updateCheckTimer = new Timer(OnUpdateTimerElapsed, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);

            // Run log cleanup and schedule for next run.
            OnCleanupLogsElapsed(null);

            // Set up our network availability checks so we can run captive portal detection on a changed network.
            NetworkChange.NetworkAddressChanged += m_dnsEnforcement.OnNetworkChange;

            // Run on startup so we can get the network state right away.
            m_dnsEnforcement.Trigger();
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
            m_logger.Info("Filter service provider process exiting.");

            try
            {
                // Unhook first.
                AppDomain.CurrentDomain.ProcessExit -= OnApplicationExiting;
            }
            catch (Exception err)
            {
                LoggerUtil.RecursivelyLogException(m_logger, err);
            }

            try
            {
                if (Environment.ExitCode == (int)ExitCodes.ShutdownWithoutSafeguards)
                {
                    m_logger.Info("Filter service provider process shutting down without safeguards.");

                    DoCleanShutdown(false);
                }
                else
                {
                    m_logger.Info("Filter service provider process shutting down with safeguards.");

                    // Unless explicitly told not to, always use safeguards.
                    DoCleanShutdown(true);
                }
            }
            catch (Exception err)
            {
                LoggerUtil.RecursivelyLogException(m_logger, err);
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
            m_systemServices.EnableInternet();

            if (e.Cancelled || e.Error != null)
            {
                m_logger.Error("Error during initialization.");
                if (e.Error != null && m_logger != null)
                {
                    LoggerUtil.RecursivelyLogException(m_logger, e.Error);
                }

                Environment.Exit((int)ExitCodes.ShutdownInitializationError);
                return;
            }

            OnUpdateTimerElapsed(null);

            Status = FilterStatus.Running;

            m_systemServices.EnsureGuiRunning(runInTray: true);
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

            var cfg = m_policyConfiguration.Configuration;

            if (cfg != null && cfg.UseThreshold)
            {
                var currentTicks = Interlocked.Increment(ref m_thresholdTicks);

                if (currentTicks >= cfg.ThresholdLimit)
                {
                    internetShutOff = true;

                    try
                    {
                        m_logger.Warn("Block action threshold met or exceeded. Disabling internet.");
                        m_systemServices.DisableInternet();
                    }
                    catch (Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }

                    this.m_thresholdEnforcementTimer.Change(cfg.ThresholdTimeoutPeriod, Timeout.InfiniteTimeSpan);
                }
            }

            string categoryNameString = "Unknown";
            var mappedCategory = m_policyConfiguration.GeneratedCategoriesMap.Values.Where(xx => xx.CategoryId == category).FirstOrDefault();

            if (mappedCategory != null)
            {
                categoryNameString = mappedCategory.CategoryName;
            }

            m_ipcServer.NotifyBlockAction(cause, requestUri, categoryNameString, DateTime.Now, matchingRule);
            m_accountability.AddBlockAction(cause, requestUri, categoryNameString, matchingRule);

            if (internetShutOff)
            {
                var restoreDate = DateTime.Now.AddTicks(cfg != null ? cfg.ThresholdTimeoutPeriod.Ticks : TimeSpan.FromMinutes(1).Ticks);

                var cooldownPeriod = (restoreDate - DateTime.Now);

                m_ipcServer.NotifyCooldownEnforced(cooldownPeriod);
            }

            m_logger.Info("Request {0} blocked by rule {1} in category {2}.", requestUri.ToString(), matchingRule, categoryNameString);
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

        /// <summary>
        /// Builds up a host from hostParts and checks the bloom filter for each entry.
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="hostParts"></param>
        /// <param name="isWhitelist"></param>
        /// <returns>true if any host is discovered in the collection.</returns>
        private bool isHostInList(FilterDbCollection collection, string[] hostParts, bool isWhitelist)
        {
            int i = hostParts.Length > 1 ? hostParts.Length - 2 : hostParts.Length - 1;
            for (; i >= 0; i--)
            {
                string checkHost = string.Join(".", new ArraySegment<string>(hostParts, i, hostParts.Length - i));
                bool result = collection.PrefetchIsDomainInList(checkHost, isWhitelist);

                if (result)
                {
                    return true;
                }
            }

            return false;
        }

        /*private async Task OnHttpResponseBegin(SessionEventArgs args)
        {
            if (args.WebSession.Response.Headers.HeaderExists("Content-Type"))
            {
                contentType = args.WebSession.Request.Headers.GetFirstHeader("Content-Type").Value;

                // This is the start of a response with a content type that we want to inspect.
                // Flag it for inspection once done. It will later call the OnHttpMessageEnd callback.
                isHtml = contentType.IndexOf("html") != -1;
                isJson = contentType.IndexOf("json") != -1;

                if (isHtml || isJson)
                {
                    nextAction = ProxyNextAction.AllowButRequestContentInspection;
                }
            }
        }*/

        private static readonly string[] htmlMimeTypes = { "text/html", "text/plain" };
        private static int nextTrackId = 1;
        private static object trackIdLock = new object();

        private void OnHttpRequestBegin(GoproxyWrapper.Session args)
        {
            int trackId = 0;
            lock(trackIdLock)
            {
                trackId = nextTrackId++;
            }

            ProxyNextAction nextAction = ProxyNextAction.AllowAndIgnoreContent;

            string customBlockResponseContentType = null;
            byte[] customBlockResponse = null;

            // Don't allow filtering if our user has been denied access and they
            // have not logged back in.
            if (m_ipcServer != null && m_ipcServer.WaitingForAuth)
            {
                return;
            }

            bool readLocked = false;

            try
            {
                ZonedDateTime date = m_timeDetection.GetRealTime();
                TimeRestrictionModel todayRestriction = null;

                if(m_policyConfiguration != null && m_policyConfiguration.TimeRestrictions != null && date != null)
                {
                    todayRestriction = m_policyConfiguration.TimeRestrictions[(int)date.ToDateTimeOffset().DayOfWeek];
                }

                // We don't know what's coming back from the server, but we can see what the client is expecting by looking at the 'Accept' header.

                Header acceptHeader = args.Request.Headers.GetFirstHeader("Accept");

                bool useHtmlBlockPage = false;

                if (acceptHeader != null)
                {
                    foreach (string headerVal in htmlMimeTypes)
                    {
                        if (acceptHeader.Value.IndexOf(headerVal, StringComparison.InvariantCultureIgnoreCase) >= 0)
                        {
                            useHtmlBlockPage = true;
                            break;
                        }
                    }
                }
                else
                {
                    // We don't know what the client is accepting. Make it obvious what's going on by spitting an error onto the screen.
                    useHtmlBlockPage = true;
                }

                if (!args.Request.Headers.HeaderExists("Referer"))
                {
                    // Use the HTML block page if this is a direct navigation.
                    useHtmlBlockPage = true;
                }

                Uri url =new Uri(args.Request.Url);

                if (todayRestriction != null && todayRestriction.RestrictionsEnabled && !m_timeDetection.IsDateTimeAllowed(date, todayRestriction))
                {
                    nextAction = ProxyNextAction.DropConnection;

                    if (useHtmlBlockPage)
                    {
                        // Only send HTML block page if we know this is a response of HTML we're blocking, or
                        // if there is no referer (direct navigation).
                        customBlockResponseContentType = "text/html";
                        customBlockResponse = getBlockPageWithResolvedTemplates(url, 0, null, BlockType.TimeRestriction);
                    }
                    else
                    {
                        customBlockResponseContentType = string.Empty;
                        customBlockResponse = null;
                    }

                    return;
                }

                var filterCollection = m_policyConfiguration.FilterCollection;
                var categoriesMap = m_policyConfiguration.GeneratedCategoriesMap;
                var categoryIndex = m_policyConfiguration.CategoryIndex;

                if (filterCollection != null)
                {
                    // Let's check whitelists first.
                    readLocked = true;
                    m_filteringRwLock.EnterReadLock();

                    List<UrlFilter> filters;
                    short matchCategory = -1;
                    UrlFilter matchingFilter = null;

                    //string host = message.Url.Host;
                    string host = url.Host;
                    string[] hostParts = host.Split('.');

                    NameValueCollection headers = new NameValueCollection();
                    foreach (var header in args.Request.Headers)
                    {
                        headers.Add(header.Name, header.Value);
                    }

                    // Check whitelists first.
                    // We build up hosts to check against the list because CheckIfFiltersApply whitelists all subdomains of a domain as well.
                    // example
                    // request for vortex.data.microsoft.com/blah comes in.
                    // we check for
                    // microsoft.com
                    // data.microsoft.com
                    // vortex.data.microsoft.com
                    // skip TLD if there is more than one part. This might have to be changed in the future,
                    // but right now we aren't blacklisting whole TLDs.
                    if (isHostInList(filterCollection, hostParts, true))
                    {
                        // domain might have filters, so we want to check for sure here.

                        filters = filterCollection.GetWhitelistFiltersForDomain(url.Host).Result;

                        if (CheckIfFiltersApply(filters, url, headers, out matchingFilter, out matchCategory))
                        {
                            var mappedCategory = categoriesMap.Values.Where(xx => xx.CategoryId == matchCategory).FirstOrDefault();

                            if (mappedCategory != null)
                            {
                                m_logger.Info("Request {0} whitelisted by rule {1} in category {2}.", url.ToString(), matchingFilter.OriginalRule, mappedCategory.CategoryName);
                            }
                            else
                            {
                                m_logger.Info("Request {0} whitelisted by rule {1} in category {2}.", url.ToString(), matchingFilter.OriginalRule, matchCategory);
                            }

                            nextAction = ProxyNextAction.AllowAndIgnoreContentAndResponse;
                            return;
                        }
                    } // else domain has no whitelist filters, continue to next check.

                    filters = filterCollection.GetWhitelistFiltersForDomain().Result;

                    if (CheckIfFiltersApply(filters, url, headers, out matchingFilter, out matchCategory))
                    {
                        var mappedCategory = categoriesMap.Values.Where(xx => xx.CategoryId == matchCategory).FirstOrDefault();

                        if (mappedCategory != null)
                        {
                            m_logger.Info("Request {0} whitelisted by rule {1} in category {2}.", url.ToString(), matchingFilter.OriginalRule, mappedCategory.CategoryName);
                        }
                        else
                        {
                            m_logger.Info("Request {0} whitelisted by rule {1} in category {2}.", url.ToString(), matchingFilter.OriginalRule, matchCategory);
                        }

                        nextAction = ProxyNextAction.AllowAndIgnoreContentAndResponse;
                        return;
                    }

                    // Since we made it this far, lets check blacklists now.

                    if (isHostInList(filterCollection, hostParts, false))
                    {
                        filters = filterCollection.GetFiltersForDomain(url.Host).Result;

                        if (CheckIfFiltersApply(filters, url, headers, out matchingFilter, out matchCategory))
                        {
                            OnRequestBlocked(matchCategory, BlockType.Url, url, matchingFilter.OriginalRule);
                            nextAction = ProxyNextAction.DropConnection;

                            // Instead of going to an external API for information, we should do everything 
                            // that we can locally.
                            List<int> matchingCategories = GetAllCategoriesMatchingUrl(filters, url, headers);
                            List<MappedFilterListCategoryModel> resolvedCategories = ResolveCategoriesFromIds(matchingCategories);

                            if (useHtmlBlockPage)
                            {
                                // Only send HTML block page if we know this is a response of HTML we're blocking, or
                                // if there is no referer (direct navigation).
                                customBlockResponseContentType = "text/html";
                                customBlockResponse = getBlockPageWithResolvedTemplates(url, matchCategory, resolvedCategories);
                            }
                            else
                            {
                                customBlockResponseContentType = string.Empty;
                                customBlockResponse = null;
                            }

                            return;
                        }
                    }

                    filters = filterCollection.GetFiltersForDomain().Result;

                    if (CheckIfFiltersApply(filters, url, headers, out matchingFilter, out matchCategory))
                    {
                        OnRequestBlocked(matchCategory, BlockType.Url, url, matchingFilter.OriginalRule);
                        nextAction = ProxyNextAction.DropConnection;

                        List<int> matchingCategories = GetAllCategoriesMatchingUrl(filters, url, headers);
                        List<MappedFilterListCategoryModel> categories = ResolveCategoriesFromIds(matchingCategories);

                        if (useHtmlBlockPage)
                        {
                            // Only send HTML block page if we know this is a response of HTML we're blocking, or
                            // if there is no referer (direct navigation).
                            customBlockResponseContentType = "text/html";
                            customBlockResponse = getBlockPageWithResolvedTemplates(url, matchCategory, categories);
                        }
                        else
                        {
                            customBlockResponseContentType = string.Empty;
                            customBlockResponse = null;
                        }

                        return;
                    }
                }
                else
                {
                    Console.WriteLine("filterCollection == null");
                }
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                if (readLocked)
                {
                    m_filteringRwLock.ExitReadLock();
                }

                if (nextAction == ProxyNextAction.DropConnection)
                {
                    // There is currently no way to change an HTTP message to a response outside of CitadelCore.
                    // so, change it to a 204 and then modify the status code to what we want it to be.
                    //m_logger.Info("Response blocked: {0}", args.Request.Url);

                    if (customBlockResponse != null)
                    {
                        args.SendCustomResponse((int)HttpStatusCode.OK, customBlockResponseContentType, customBlockResponse);
                    }
                }
            }
        }

        private void OnBeforeResponse(GoproxyWrapper.Session args)
        {
            ProxyNextAction nextAction = ProxyNextAction.AllowButRequestContentInspection;

            bool shouldBlock = false;
            string customBlockResponseContentType = null;
            byte[] customBlockResponse = null;

            // Don't allow filtering if our user has been denied access and they
            // have not logged back in.
            if (m_ipcServer != null && m_ipcServer.WaitingForAuth)
            {
                return;
            }

            // The only thing we can really do in this callback, and the only thing we care to do, is
            // try to classify the content of the response payload, if there is any.
            try
            {
                var uri = new Uri(args.Request.Url);

                // Check our certificate exemptions to see if we should allow this site through or not.
                if (args.Response.CertificateCount > 0 && !args.Response.IsCertificateVerified && !m_certificateExemptions.IsExempted(uri.Host, args.Response.Certificates[0]))
                {
                    customBlockResponseContentType = "text/html";
                    customBlockResponse = getBadSslPageWithResolvedTemplates(new Uri(args.Request.Url), args.Response.Certificates[0].Thumbprint, Encoding.UTF8.GetString(m_badSslHtmlPage));
                    nextAction = ProxyNextAction.DropConnection;
                    return;
                }

                string contentType = null;
                if (args.Response.Headers.HeaderExists("Content-Type"))
                {
                    contentType = args.Response.Headers.GetFirstHeader("Content-Type").Value;

                    bool isHtml = contentType.IndexOf("html") != -1;
                    bool isJson = contentType.IndexOf("json") != -1;
                    bool isTextPlain = contentType.IndexOf("text/plain") != -1;

                    // Is the response content type text/html or application/json? Inspect it, otherwise return before we do content classification.
                    // Why enforce content classification on only these two? There are only a few MIME types which have a high risk of "carrying" explicit content.
                    // Those are:
                    // text/plain
                    // text/html
                    // application/json
                    if (!(isHtml || isJson || isTextPlain))
                    {
                        return;
                    }
                }

                if (contentType != null && args.Response.HasBody)
                {
                    contentType = contentType.ToLower();

                    BlockType blockType;
                    string textTrigger;
                    string textCategory;

                    byte[] responseBody = args.Response.Body;
                    var contentClassResult = OnClassifyContent(responseBody, contentType, out blockType, out textTrigger, out textCategory);

                    if (contentClassResult > 0)
                    {
                        shouldBlock = true;

                        List<MappedFilterListCategoryModel> categories = new List<MappedFilterListCategoryModel>();

                        if (contentType.IndexOf("html") != -1)
                        {
                            customBlockResponseContentType = "text/html";
                            customBlockResponse = getBlockPageWithResolvedTemplates(new Uri(args.Request.Url), contentClassResult, categories, blockType, textCategory);
                            nextAction = ProxyNextAction.DropConnection;
                        }

                        OnRequestBlocked(contentClassResult, blockType, new Uri(args.Request.Url));
                        m_logger.Info("Response blocked by content classification.");
                    }
                }
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                if (nextAction == ProxyNextAction.DropConnection)
                {
                    // TODO: Do we really need this as Info?
                    m_logger.Info("Response blocked: {0}", args.Request.Url);

                    if (customBlockResponse != null)
                    {
                        args.SendCustomResponse((int)HttpStatusCode.OK, customBlockResponseContentType, customBlockResponse);
                    }
                }
            }
        }

        private void OnHttpWholeBodyResponseInspection(HttpMessageInfo message)
        {
            bool shouldBlock = false;
            string customBlockResponseContentType = null;
            byte[] customBlockResponse = null;

            // Don't allow filtering if our user has been denied access and they
            // have not logged back in.
            if (m_ipcServer != null && m_ipcServer.WaitingForAuth)
            {
                return;
            }

            // The only thing we can really do in this callback, and the only thing we care to do, is
            // try to classify the content of the response payload, if there is any.
            try
            {
                string contentType = null;

                if ((contentType = message.Headers["Content-Type"]) != null)
                {
                    contentType = contentType.ToLower();

                    BlockType blockType;
                    string textTrigger;
                    string textCategory;

                    var contentClassResult = OnClassifyContent(message.Body, contentType, out blockType, out textTrigger, out textCategory);

                    if (contentClassResult > 0)
                    {
                        shouldBlock = true;

                        List<MappedFilterListCategoryModel> categories = new List<MappedFilterListCategoryModel>();

                        if (contentType.IndexOf("html") != -1)
                        {
                            customBlockResponseContentType = "text/html";
                            customBlockResponse = getBlockPageWithResolvedTemplates(message.Url, contentClassResult, categories, blockType, textCategory);
                            message.ProxyNextAction = ProxyNextAction.DropConnection;
                        }

                        OnRequestBlocked(contentClassResult, blockType, message.Url);
                        m_logger.Info("Response blocked by content classification.");
                    }
                }
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                if (message.ProxyNextAction == ProxyNextAction.DropConnection)
                {
                    m_logger.Info("Response blocked: {0}", message.Url);

                    message.Make204NoContent();

                    if (customBlockResponse != null)
                    {
                        message.CopyAndSetBody(customBlockResponse, 0, customBlockResponse.Length, customBlockResponseContentType);
                        message.StatusCode = HttpStatusCode.OK;

                        m_logger.Info("Writing custom block response: {0} {1} {2}", message.Url, message.StatusCode, customBlockResponse.Length);
                    }
                }
            }
        }

        private bool CheckIfFiltersApply(List<UrlFilter> filters, Uri request, NameValueCollection headers, out UrlFilter matched, out short matchedCategory)
        {
            matchedCategory = -1;
            matched = null;

            var len = filters.Count;
            for (int i = 0; i < len; ++i)
            {
                if (m_policyConfiguration.CategoryIndex.GetIsCategoryEnabled(filters[i].CategoryId) && filters[i].IsMatch(request, headers))
                {
                    matched = filters[i];
                    matchedCategory = filters[i].CategoryId;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Use this function after you've determined that the filter should block a certain URI.
        /// </summary>
        /// <param name="filters"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        private List<int> GetAllCategoriesMatchingUrl(List<UrlFilter> filters, Uri request, NameValueCollection headers)
        {
            List<int> matchingCategories = new List<int>();

            var len = filters.Count;
            for (int i = 0; i < len; i++)
            {
                if (m_policyConfiguration.CategoryIndex.GetIsCategoryEnabled(filters[i].CategoryId) && filters[i].IsMatch(request, headers))
                {
                    matchingCategories.Add(filters[i].CategoryId);
                }
            }

            return matchingCategories;
        }

        private List<MappedFilterListCategoryModel> ResolveCategoriesFromIds(List<int> matchingCategories)
        {
            List<MappedFilterListCategoryModel> categories = new List<MappedFilterListCategoryModel>();

            int length = matchingCategories.Count;
            var categoryValues = m_policyConfiguration.GeneratedCategoriesMap.Values;
            foreach (var category in categoryValues)
            {
                for (int i = 0; i < length; i++)
                {
                    if (category.CategoryId == matchingCategories[i])
                    {
                        categories.Add(category);
                    }
                }
            }

            return categories;
        }

        private byte[] getBadSslPageWithResolvedTemplates(Uri requestUri, string certThumbprint, string pageTemplate)
        {
            // Produces something that looks like "www.badsite.com/example?arg=0" instead of "http://www.badsite.com/example?arg=0"
            // IMO this looks slightly more friendly to a user than the entire URI.
            string friendlyUrlText = (requestUri.Host + requestUri.PathAndQuery + requestUri.Fragment).TrimEnd('/');
            string urlText = requestUri.ToString();

            urlText = urlText == null ? "" : urlText;

            pageTemplate = pageTemplate.Replace("{{url_text}}", urlText);
            pageTemplate = pageTemplate.Replace("{{friendly_url_text}}", friendlyUrlText);
            pageTemplate = pageTemplate.Replace("{{host}}", requestUri.Host);
            pageTemplate = pageTemplate.Replace("{{certThumbprintExists}}", certThumbprint == null ? "false" : "true");
            pageTemplate = pageTemplate.Replace("{{certThumbprint}}", certThumbprint);

            return Encoding.UTF8.GetBytes(pageTemplate);
        }

        private byte[] getBlockPageWithResolvedTemplates(Uri requestUri, int matchingCategory, List<MappedFilterListCategoryModel> appliedCategories, BlockType blockType = BlockType.None, string triggerCategory = "")
        {
            string blockPageTemplate = UTF8Encoding.Default.GetString(m_blockedHtmlPage);

            // Produces something that looks like "www.badsite.com/example?arg=0" instead of "http://www.badsite.com/example?arg=0"
            // IMO this looks slightly more friendly to a user than the entire URI.
            string friendlyUrlText = (requestUri.Host + requestUri.PathAndQuery + requestUri.Fragment).TrimEnd('/');
            string urlText = requestUri.ToString();

            string deviceName;

            try
            {
                deviceName = Environment.MachineName;
            }
            catch
            {
                deviceName = "Unknown";
            }

            string blockedRequestBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(urlText));

            string unblockRequest = WebServiceUtil.Default.ServiceProviderUnblockRequestPath;
            string username = WebServiceUtil.Default.UserEmail ?? "DNS";

            string query = string.Format("category_name=LOOKUP_UNKNOWN&user_id={0}&device_name={1}&blocked_request={2}", Uri.EscapeDataString(username), deviceName, Uri.EscapeDataString(blockedRequestBase64));
            unblockRequest += "?" + query;

            string message = "was blocked because it was in the following category:";
            string unblockRequestButton = "<a class=\"request-unblock button primary\" href=\"{{unblock_request}}\">Request an Unblock</a>";
            string relaxed_policy_message = "";
            string matching_category = "";
            string otherCategories = "";

            // Determine if URL is in the relaxed policy.
            foreach (var entry in m_policyConfiguration.GeneratedCategoriesMap.Values)
            {
                if (matchingCategory == entry.CategoryId)
                {
                    matching_category = entry.ShortCategoryName;
                }
                else if (appliedCategories?.Any(m => m.CategoryId == entry.CategoryId) == true)
                {
                    otherCategories += $"<p class='category other'>{entry.ShortCategoryName}</p>";
                }

                if (entry is MappedBypassListCategoryModel)
                {
                    if (matchingCategory == entry.CategoryId)
                    {
                        relaxed_policy_message = "<p style='margin-top: 10px;'>This site is allowed by the relaxed policy. To access it, open CloudVeil for Windows, go to settings, then click 'use relaxed policy'</p>";
                        break;
                    }
                }
            }

            // Get category or block type.
            string url_text = urlText == null ? "" : urlText;
            if (matchingCategory > 0 && blockType == BlockType.None)
            {
                // matching_category name already set.
            }
            else
            {
                otherCategories = "";

                switch (blockType)
                {
                    case BlockType.None:
                        matching_category = "unknown reason";
                        break;

                    case BlockType.ImageClassification:
                        matching_category = "naughty image";
                        break;

                    case BlockType.Url:
                        matching_category = "bad webpage";
                        break;

                    case BlockType.TextClassification:
                    case BlockType.TextTrigger:
                        matching_category = string.Format("offensive text: {0}", triggerCategory);
                        break;

                    case BlockType.TimeRestriction:
                        message = "is blocked because your time restrictions do not allow internet access at this time.";
                        //matching_category = "no internet allowed after hours";
                        unblockRequestButton = "";
                        break;

                    case BlockType.OtherContentClassification:
                    default:
                        matching_category = "other content classification";
                        break;
                }
            }

            blockPageTemplate = blockPageTemplate.Replace("{{url_text}}", url_text);
            blockPageTemplate = blockPageTemplate.Replace("{{friendly_url_text}}", friendlyUrlText);
            blockPageTemplate = blockPageTemplate.Replace("{{message}}", message);
            blockPageTemplate = blockPageTemplate.Replace("{{matching_category}}", matching_category);
            blockPageTemplate = blockPageTemplate.Replace("{{other_categories}}", otherCategories);
            blockPageTemplate = blockPageTemplate.Replace("{{unblock_request_button}}", unblockRequestButton);
            blockPageTemplate = blockPageTemplate.Replace("{{unblock_request}}", unblockRequest);
            blockPageTemplate = blockPageTemplate.Replace("{{relaxed_policy_message}}", relaxed_policy_message);

            return Encoding.UTF8.GetBytes(blockPageTemplate);
        }

        private NameValueCollection ParseHeaders(string headers)
        {
            var nvc = new NameValueCollection(StringComparer.OrdinalIgnoreCase);

            using (var reader = new StringReader(headers))
            {
                string line = null;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var firstSplitIndex = line.IndexOf(':');
                    if (firstSplitIndex == -1)
                    {
                        nvc.Add(line.Trim(), string.Empty);
                    }
                    else
                    {
                        nvc.Add(line.Substring(0, firstSplitIndex).Trim(), line.Substring(firstSplitIndex + 1).Trim());
                    }
                }
            }

            return nvc;
        }

        /// <summary>
        /// Called by the engine when the engine fails to classify a request or response by its
        /// metadata. The engine provides a full byte array of the content of the request or
        /// response, along with the declared content type of the data. This is currently used for
        /// NLP classification, but can be adapted with minimal changes to the Engine.
        /// </summary>
        /// <param name="data">
        /// The data to be classified. 
        /// </param>
        /// <param name="contentType">
        /// The declared content type of the data. 
        /// </param>
        /// <returns>
        /// A numeric category ID that the content was deemed to belong to. Zero is returned here if
        /// the content is not deemed to be part of any known category, which is a general indication
        /// to the engine that the content should not be blocked.
        /// </returns>
        private short OnClassifyContent(Memory<byte> data, string contentType, out BlockType blockedBecause, out string textTrigger, out string triggerCategory)
        {
            Stopwatch stopwatch = null;

            try
            {
                m_filteringRwLock.EnterReadLock();

                stopwatch = Stopwatch.StartNew();
                if (m_policyConfiguration.TextTriggers != null && m_policyConfiguration.TextTriggers.HasTriggers)
                {
                    var isHtml = contentType.IndexOf("html") != -1;
                    var isJson = contentType.IndexOf("json") != -1;
                    if (isHtml || isJson)
                    {
                        var dataToAnalyzeStr = Encoding.UTF8.GetString(data.ToArray());

                        if (isHtml)
                        {
                            // This doesn't work anymore because google has started sending bad stuff directly
                            // embedded inside HTML responses, instead of sending JSON a separate response.
                            // So, we need to let the triggers engine just chew through the entire raw HTML.
                            // var ext = new FastHtmlTextExtractor();
                            // dataToAnalyzeStr = ext.Extract(dataToAnalyzeStr.ToCharArray(), true);
                        }

                        short matchedCategory = -1;
                        string trigger = null;
                        var cfg = m_policyConfiguration.Configuration;

                        if (m_policyConfiguration.TextTriggers.ContainsTrigger(dataToAnalyzeStr, out matchedCategory, out trigger, m_policyConfiguration.CategoryIndex.GetIsCategoryEnabled, cfg != null && cfg.MaxTextTriggerScanningSize > 1, cfg != null ? cfg.MaxTextTriggerScanningSize : -1))
                        {
                            m_logger.Info("Triggers successfully run. matchedCategory = {0}, trigger = '{1}'", matchedCategory, trigger);

                            var mappedCategory = m_policyConfiguration.GeneratedCategoriesMap.Values.Where(xx => xx.CategoryId == matchedCategory).FirstOrDefault();

                            if (mappedCategory != null)
                            {
                                m_logger.Info("Response blocked by text trigger \"{0}\" in category {1}.", trigger, mappedCategory.CategoryName);
                                blockedBecause = BlockType.TextTrigger;
                                triggerCategory = mappedCategory.CategoryName;
                                textTrigger = trigger;
                                return mappedCategory.CategoryId;
                            }
                        }
                    }
                }
                stopwatch.Stop();

                //m_logger.Info("Text triggers took {0} on {1}", stopwatch.ElapsedMilliseconds, url);
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                m_filteringRwLock.ExitReadLock();
            }

#if WITH_NLP
            try
            {
                m_doccatSlimLock.EnterReadLock();

                contentType = contentType.ToLower();

                // Only attempt text classification if we have a text classifier, silly.
                if(m_documentClassifiers != null && m_documentClassifiers.Count > 0)
                {
                    var textToClassifyBuilder = new StringBuilder();

                    if(contentType.IndexOf("html") != -1)
                    {
                        // This might be plain text, might be HTML. We need to find out.
                        var rawText = Encoding.UTF8.GetString(data).ToCharArray();

                        var extractor = new FastHtmlTextExtractor();

                        var extractedText = extractor.Extract(rawText);
                        m_logger.Info("From HTML: Classify this string: {0}", extractedText);
                        textToClassifyBuilder.Append(extractedText);
                    }
                    else if(contentType.IndexOf("json") != -1)
                    {
                        // This should be JSON.
                        var jsonText = Encoding.UTF8.GetString(data);

                        var len = jsonText.Length;
                        for(int i = 0; i < len; ++i)
                        {
                            char c = jsonText[i];
                            if(char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                            {
                                textToClassifyBuilder.Append(c);
                            }
                            else
                            {
                                textToClassifyBuilder.Append(' ');
                            }
                        }

                        m_logger.Info("From Json: Classify this string: {0}", m_whitespaceRegex.Replace(textToClassifyBuilder.ToString(), " "));
                    }

                    var textToClassify = textToClassifyBuilder.ToString();

                    if(textToClassify.Length > 0)
                    {
                        foreach(var classifier in m_documentClassifiers)
                        {
                            m_logger.Info("Got text to classify of length {0}.", textToClassify.Length);

                            // Remove all multi-whitespace, newlines etc.
                            textToClassify = m_whitespaceRegex.Replace(textToClassify, " ");

                            var classificationResult = classifier.ClassifyText(textToClassify);

                            MappedFilterListCategoryModel categoryNumber = null;

                            if(m_generatedCategoriesMap.TryGetValue(classificationResult.BestCategoryName, out categoryNumber))
                            {
                                if(categoryNumber.CategoryId > 0 && m_categoryIndex.GetIsCategoryEnabled(categoryNumber.CategoryId))
                                {
                                    var cfg = m_policyConfiguration.Configuration;
                                    var threshold = cfg != null ? cfg.NlpThreshold : 0.9f;

                                    if(classificationResult.BestCategoryScore < threshold)
                                    {
                                        m_logger.Info("Rejected {0} classification because score was less than threshold of {1}. Returned score was {2}.", classificationResult.BestCategoryName, threshold, classificationResult.BestCategoryScore);
                                        blockedBecause = BlockType.OtherContentClassification;
                                        return 0;
                                    }

                                    m_logger.Info("Classified text content as {0}.", classificationResult.BestCategoryName);
                                    blockedBecause = BlockType.TextClassification;
                                    return categoryNumber.CategoryId;
                                }
                            }
                            else
                            {
                                m_logger.Info("Did not find category registered: {0}.", classificationResult.BestCategoryName);
                            }
                        }
                    }
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                m_doccatSlimLock.ExitReadLock();
            }

#endif
            // Default to zero. Means don't block this content.
            blockedBecause = BlockType.OtherContentClassification;
            textTrigger = "";
            triggerCategory = "";
            return 0;
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
                Interlocked.Exchange(ref m_thresholdTicks, 0);

                var cfg = m_policyConfiguration.Configuration;

                this.m_thresholdCountTimer.Change(cfg != null ? cfg.ThresholdTriggerPeriod : TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ex);
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
                m_systemServices.EnableInternet();
            }
            catch (Exception e)
            {
                m_logger.Warn("Error when trying to reinstate internet on threshold timeout period elapsed.");
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }

            Status = FilterStatus.Running;

            // Disable the timer before we leave.
            this.m_thresholdEnforcementTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public UpdateCheckResult CheckForApplicationUpdate(bool isCheckButton)
        {
            m_logger.Info("Checking for application updates.");
            return m_updateSystem.ProbeMasterForApplicationUpdates(isCheckButton);
        }

        public ConfigUpdateResult UpdateAndWriteList(bool isSyncButton)
        {
            LogTime("UpdateAndWriteList");

            ConfigUpdateResult result = ConfigUpdateResult.ErrorOccurred;

            try
            {
                m_logger.Info("Checking for filter list updates.");

                m_updateRwLock.EnterWriteLock();

                bool? configurationDownloaded = m_policyConfiguration.DownloadConfiguration();

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

                bool doLoadLists = m_policyConfiguration.FilterCollection == null;

                if (m_policyConfiguration.Configuration == null || configurationDownloaded == true || (configurationDownloaded == null && m_policyConfiguration.Configuration == null))
                {
                    // Got new data. Gotta reload.
                    bool configLoaded = m_policyConfiguration.LoadConfiguration();
                    doLoadLists = true;

                    result = ConfigUpdateResult.Updated;

                    // Enforce DNS if present.
                    m_dnsEnforcement.Trigger();
                }

                bool? listsDownloaded = m_policyConfiguration.DownloadLists();

                doLoadLists = doLoadLists || listsDownloaded == true || (listsDownloaded == null && m_policyConfiguration.FilterCollection == null);

                if (doLoadLists)
                {
                    m_policyConfiguration.LoadLists();
                }
                else if (listsDownloaded == null && m_policyConfiguration.Configuration == null)
                {
                    m_logger.Error("Was not able to download rulesets due to configuration being null");
                }

            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                // We don't handle all cases in the switch, because we don't want to set LastSettingsCheck for error states.
                switch(result)
                {
                    case ConfigUpdateResult.Updated:
                    case ConfigUpdateResult.UpToDate:
                        AppSettings.Default.LastSettingsCheck = DateTime.Now;
                        break;
                }

                AppSettings.Default.ConfigUpdateResult = result;
                AppSettings.Default.Save();

                m_ipcServer.Send<ConfigCheckInfo>(IpcCall.SynchronizeSettings, new ConfigCheckInfo(DateTime.Now, result));

                // Enable the timer again.
                if (!(NetworkStatus.Default.HasIpv4InetConnection || NetworkStatus.Default.HasIpv6InetConnection))
                {
                    // If we have no internet, keep polling every 15 seconds. We need that data ASAP.
                    this.m_updateCheckTimer.Change(TimeSpan.FromSeconds(15), Timeout.InfiniteTimeSpan);
                }
                else
                {
                    var cfg = m_policyConfiguration.Configuration;
                    if (cfg != null)
                    {
                        this.m_updateCheckTimer.Change(cfg.UpdateFrequency, Timeout.InfiniteTimeSpan);
                    }
                    else
                    {
                        this.m_updateCheckTimer.Change(TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
                    }
                }

                m_updateRwLock.ExitWriteLock();
            }

            return result;
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
            m_logger.Info("Running OnUpdateTimerElapsed");

            if (m_ipcServer != null && m_ipcServer.WaitingForAuth)
            {
                return;
            }

            this.UpdateAndWriteList(false);
            this.CheckForApplicationUpdate(false);

            this.CleanupLogs();

            if (m_lastUsernamePrintTime.Date < DateTime.Now.Date)
            {
                m_lastUsernamePrintTime = DateTime.Now;
                m_logger.Info($"Currently logged in user is {WebServiceUtil.Default.UserEmail}");
            }
        }

        public const int LogCleanupIntervalInHours = 12;
        public const int MaxLogAgeInDays = 7;

        private void OnCleanupLogsElapsed(object state)
        {
            this.CleanupLogs();

            if (m_cleanupLogsTimer == null)
            {
                m_cleanupLogsTimer = new Timer(OnCleanupLogsElapsed, null, TimeSpan.FromHours(LogCleanupIntervalInHours), Timeout.InfiniteTimeSpan);
            }
            else
            {
                m_cleanupLogsTimer.Change(TimeSpan.FromHours(LogCleanupIntervalInHours), Timeout.InfiniteTimeSpan);
            }
        }

        Stopwatch m_logTimeStopwatch = null;
        /// <summary>
        /// Logs the amount of time that has passed since the last time this function was called.
        /// </summary>
        /// <param name="message"></param>
        private void LogTime(string message)
        {
            string timeInfo = null;

            if (m_logTimeStopwatch == null)
            {
                m_logTimeStopwatch = Stopwatch.StartNew();
                timeInfo = "Initialized:";
            }
            else
            {
                long ms = m_logTimeStopwatch.ElapsedMilliseconds;
                timeInfo = string.Format("{0}ms:", ms);

                m_logTimeStopwatch.Restart();
            }

            m_logger.Info("TIME {0} {1}", timeInfo, message);
        }

        private void CleanupLogs()
        {
            try
            {
                string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "logs");

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
                LoggerUtil.RecursivelyLogException(m_logger, ex);
                // TODO: Tell sentry about this.
            }
        }

        /// <summary>
        /// Starts the filtering engine. 
        /// </summary>
        private void StartFiltering()
        {
            m_logger.Info(nameof(StartFiltering));
            // Let's make sure we've pulled our internet block.
            try
            {
                m_systemServices.EnableInternet();
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ex);
            }

            try
            {
                if (m_filteringEngine != null && !m_filteringEngine.IsRunning)
                {
                    m_logger.Info("Start engine.");

                    // Start the engine right away, to ensure the atomic bool IsRunning is set.
                    m_filteringEngine.Start();
                }
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        public class RelaxedPolicyResponseObject
        {
            public bool allowed { get; set; }
            public string message { get; set; }
            public int used { get; set; }
            public int permitted { get; set; }
        }

        /// <summary>
        /// Whenever the config is reloaded, sync the bypasses from the server.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnConfigLoaded_LoadRelaxedPolicy(object sender, EventArgs e)
        {
            try
            {
                this.UpdateNumberOfBypassesFromServer();
            }
            catch (Exception ex)
            {
                // TODO: Tell Sentry about this.
                LoggerUtil.RecursivelyLogException(m_logger, ex);
            }
        }

        /// <summary>
        /// Called whenever a relaxed policy has been requested. 
        /// </summary>
        private void OnRelaxedPolicyRequested(RelaxedPolicyEventArgs args)
        {
            HttpStatusCode statusCode;

            var parameters = new Dictionary<string, object>();
            if(args.Passcode != null)
            {
                parameters.Add("passcode", args.Passcode);
            }

            byte[] bypassResponse = WebServiceUtil.Default.RequestResource(ServiceResource.BypassRequest, out statusCode, parameters);

            bool useLocalBypassLogic = false;

            bool grantBypass = false;
            string bypassNotification = "";

            int bypassesUsed = 0;
            int bypassesPermitted = 0;

            if (bypassResponse != null)
            {
                if (statusCode == HttpStatusCode.NotFound)
                {
                    // Fallback on local bypass logic if server does not support relaxed policy checks.
                    useLocalBypassLogic = true;
                }

                string jsonString = Encoding.UTF8.GetString(bypassResponse);
                m_logger.Info("Response received {0}: {1}", statusCode.ToString(), jsonString);

                var bypassObject = JsonConvert.DeserializeObject<RelaxedPolicyResponseObject>(jsonString);

                if (bypassObject.allowed)
                {
                    grantBypass = true;
                }
                else
                {
                    grantBypass = false;
                    bypassNotification = bypassObject.message;
                }

                bypassesUsed = bypassObject.used;
                bypassesPermitted = bypassObject.permitted;
            }
            else
            {
                m_logger.Info("No response detected.");

                useLocalBypassLogic = false;
                grantBypass = false;
            }

            if (useLocalBypassLogic)
            {
                m_logger.Info("Using local bypass logic since server does not yet support bypasses.");

                // Start the count down timer.
                if (m_relaxedPolicyExpiryTimer == null)
                {
                    m_relaxedPolicyExpiryTimer = new Timer(OnRelaxedPolicyTimerExpired, null, Timeout.Infinite, Timeout.Infinite);
                }

                // Disable every category that is a bypass category.
                foreach (var entry in m_policyConfiguration.GeneratedCategoriesMap.Values)
                {
                    if (entry is MappedBypassListCategoryModel)
                    {
                        m_policyConfiguration.CategoryIndex.SetIsCategoryEnabled(((MappedBypassListCategoryModel)entry).CategoryId, false);
                    }
                }

                var cfg = m_policyConfiguration.Configuration;
                m_relaxedPolicyExpiryTimer.Change(cfg != null ? cfg.BypassDuration : TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);

                DecrementRelaxedPolicy_Local();
            }
            else
            {
                if (grantBypass)
                {
                    m_logger.Info("Relaxed policy granted.");

                    // Start the count down timer.
                    if (m_relaxedPolicyExpiryTimer == null)
                    {
                        m_relaxedPolicyExpiryTimer = new Timer(OnRelaxedPolicyTimerExpired, null, Timeout.Infinite, Timeout.Infinite);
                    }

                    // Disable every category that is a bypass category.
                    foreach (var entry in m_policyConfiguration.GeneratedCategoriesMap.Values)
                    {
                        if (entry is MappedBypassListCategoryModel)
                        {
                            m_policyConfiguration.CategoryIndex.SetIsCategoryEnabled(((MappedBypassListCategoryModel)entry).CategoryId, false);
                        }
                    }

                    var cfg = m_policyConfiguration.Configuration;
                    m_relaxedPolicyExpiryTimer.Change(cfg != null ? cfg.BypassDuration : TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);

                    DecrementRelaxedPolicy(bypassesUsed, bypassesPermitted, cfg != null ? cfg.BypassDuration : TimeSpan.FromMinutes(5));
                }
                else
                {
                    var cfg = m_policyConfiguration.Configuration;

                    RelaxedPolicyStatus status = RelaxedPolicyStatus.AllUsed;
                    if (bypassNotification.IndexOf("incorrect passcode", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        status = RelaxedPolicyStatus.Unauthorized;
                    }

                    m_ipcServer.NotifyRelaxedPolicyChange(bypassesPermitted - bypassesUsed, cfg != null ? cfg.BypassDuration : TimeSpan.FromMinutes(5), status);
                }
            }
        }

        private void DecrementRelaxedPolicy(int bypassesUsed, int bypassesPermitted, TimeSpan bypassDuration)
        {
            bool allUsesExhausted = (bypassesUsed >= bypassesPermitted);

            if (allUsesExhausted)
            {
                m_logger.Info("All uses exhausted.");

                m_ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero, RelaxedPolicyStatus.AllUsed);
            }
            else
            {
                m_ipcServer.NotifyRelaxedPolicyChange(bypassesPermitted - bypassesUsed, bypassDuration, RelaxedPolicyStatus.Granted);
            }

            if (allUsesExhausted)
            {
                // Reset our bypasses at 8:15 UTC.
                var resetTime = DateTime.UtcNow.Date.AddHours(8).AddMinutes(15);

                var span = resetTime - DateTime.UtcNow;

                if (m_relaxedPolicyResetTimer == null)
                {
                    m_relaxedPolicyResetTimer = new Timer(OnRelaxedPolicyResetExpired, null, span, Timeout.InfiniteTimeSpan);
                }

                m_relaxedPolicyResetTimer.Change(span, Timeout.InfiniteTimeSpan);
            }
        }

        private void DecrementRelaxedPolicy_Local()
        {
            bool allUsesExhausted = false;

            var cfg = m_policyConfiguration.Configuration;

            if (cfg != null)
            {
                cfg.BypassesUsed++;

                allUsesExhausted = cfg.BypassesPermitted - cfg.BypassesUsed <= 0;

                if (allUsesExhausted)
                {
                    m_ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero, RelaxedPolicyStatus.AllUsed);
                }
                else
                {
                    m_ipcServer.NotifyRelaxedPolicyChange(cfg.BypassesPermitted - cfg.BypassesUsed, cfg.BypassDuration, RelaxedPolicyStatus.Granted);
                }
            }
            else
            {
                m_ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero, RelaxedPolicyStatus.Granted);
            }

            if (allUsesExhausted)
            {
                // Refresh tomorrow at midnight.
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var span = tomorrow - DateTime.Now;

                if (m_relaxedPolicyResetTimer == null)
                {
                    m_relaxedPolicyResetTimer = new Timer(OnRelaxedPolicyResetExpired, null, span, Timeout.InfiniteTimeSpan);
                }

                m_relaxedPolicyResetTimer.Change(span, Timeout.InfiniteTimeSpan);
            }
        }

        private RelaxedPolicyStatus getRelaxedPolicyStatus()
        {
            bool relaxedInEffect = false;

            if (m_policyConfiguration.GeneratedCategoriesMap != null)
            {
                // Determine if a relaxed policy is currently in effect.
                foreach (var entry in m_policyConfiguration.GeneratedCategoriesMap.Values)
                {
                    if (entry is MappedBypassListCategoryModel)
                    {
                        if (m_policyConfiguration.CategoryIndex.GetIsCategoryEnabled(((MappedBypassListCategoryModel)entry).CategoryId) == false)
                        {
                            relaxedInEffect = true;
                        }
                    }
                }
            }

            if (relaxedInEffect)
            {
                return RelaxedPolicyStatus.Activated;
            }
            else
            {
                if (m_policyConfiguration.Configuration != null && m_policyConfiguration.Configuration.BypassesPermitted - m_policyConfiguration.Configuration.BypassesUsed == 0)
                {
                    return RelaxedPolicyStatus.AllUsed;
                }
                else
                {
                    return RelaxedPolicyStatus.Deactivated;
                }
            }
        }

        /// <summary>
        /// Called when the user has manually requested to relinquish a relaxed policy. 
        /// </summary>
        private void OnRelinquishRelaxedPolicyRequested()
        {
            RelaxedPolicyStatus status = getRelaxedPolicyStatus();

            // Ensure timer is stopped and re-enable categories by simply calling the timer's expiry callback.
            if (status == RelaxedPolicyStatus.Activated)
            {
                OnRelaxedPolicyTimerExpired(null);
            }

            // We want to inform the user that there is no relaxed policy in effect currently for this installation.
            if (status == RelaxedPolicyStatus.Deactivated)
            {
                var cfg = m_policyConfiguration.Configuration;
                m_ipcServer.NotifyRelaxedPolicyChange(cfg.BypassesPermitted - cfg.BypassesUsed, cfg.BypassDuration, RelaxedPolicyStatus.AlreadyRelinquished);
            }
        }

        /// <summary>
        /// Called whenever the relaxed policy duration has expired. 
        /// </summary>
        /// <param name="state">
        /// Async state. Not used. 
        /// </param>
        private void OnRelaxedPolicyTimerExpired(object state)
        {
            try
            {
                // Enable every category that is a bypass category.
                foreach (var entry in m_policyConfiguration.GeneratedCategoriesMap.Values)
                {
                    if (entry is MappedBypassListCategoryModel)
                    {
                        m_policyConfiguration.CategoryIndex.SetIsCategoryEnabled(((MappedBypassListCategoryModel)entry).CategoryId, true);
                    }
                }

                // Disable the expiry timer.
                m_relaxedPolicyExpiryTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ex);
            }
        }

        private bool UpdateNumberOfBypassesFromServer()
        {
            HttpStatusCode statusCode;
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("check_only", "1");

            byte[] response = WebServiceUtil.Default.RequestResource(ServiceResource.BypassRequest, out statusCode, parameters);

            if (response == null)
            {
                return false;
            }

            string responseString = Encoding.UTF8.GetString(response);

            var bypassInfo = JsonConvert.DeserializeObject<RelaxedPolicyResponseObject>(responseString);

            m_logger.Info("Bypass info: {0}/{1}", bypassInfo.used, bypassInfo.permitted);
            var cfg = m_policyConfiguration.Configuration;

            if (cfg != null)
            {
                cfg.BypassesUsed = bypassInfo.used;
                cfg.BypassesPermitted = bypassInfo.permitted;
            }

            m_ipcServer.NotifyRelaxedPolicyChange(bypassInfo.permitted - bypassInfo.used, cfg != null ? cfg.BypassDuration : TimeSpan.FromMinutes(5), getRelaxedPolicyStatus());
            return true;
        }

        /// <summary>
        /// Called whenever the relaxed policy reset timer has expired. This expiry refreshes the
        /// available relaxed policy requests to the configured value.
        /// </summary>
        /// <param name="state">
        /// Async state. Not used. 
        /// </param>
        private void OnRelaxedPolicyResetExpired(object state)
        {
            try
            {
                UpdateNumberOfBypassesFromServer();
                // Disable the reset timer.
                m_relaxedPolicyResetTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                // TODO: Tell sentry about this.
                LoggerUtil.RecursivelyLogException(m_logger, ex);
            }
        }

        /// <summary>
        /// Occurs when the filtering engine is stopped.
        /// </summary>
        public event EventHandler OnStopFiltering;

        /// <summary>
        /// Stops the filtering engine, shuts it down. 
        /// </summary>
        private void StopFiltering()
        {
            if (m_filteringEngine != null && m_filteringEngine.IsRunning)
            {
                m_filteringEngine.Stop();
            }

            try
            {
                OnStopFiltering?.Invoke(null, null);
            }
            catch (Exception e)
            {
                m_logger.Error("Error occurred in OnStopFiltering event");
                LoggerUtil.RecursivelyLogException(m_logger, e);
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
            m_systemServices.KillAllGuis();

            lock (m_cleanShutdownLock)
            {
                if (!m_cleanShutdownComplete)
                {
                    m_ipcServer.Dispose();

                    try
                    {
                        // Pull our critical status.
                        PlatformTypes.New<IAntitampering>().DisableProcessProtection();
                    }
                    catch (Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }

                    try
                    {
                        // Shut down engine.
                        StopFiltering();
                    }
                    catch (Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
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
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }

                        try
                        {
                            var cfg = m_policyConfiguration.Configuration;
                            if (cfg != null && cfg.BlockInternet)
                            {
                                // While we're here, let's disable the internet so that the user
                                // can't browse the web without us. Only do this of course if configured.
                                try
                                {
                                    m_systemServices.DisableInternet();
                                }
                                catch { }
                            }
                        }
                        catch (Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }
                    else
                    {
                        // Means that our user got a granted deactivation request, or installed but
                        // never activated.
                        m_logger.Info("Shutting down without safeguards.");
                    }

                    // Flag that clean shutdown was completed already.
                    m_cleanShutdownComplete = true;
                }
            }
        }
    }
}
