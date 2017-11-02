/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Extensions;
using Citadel.Core.WinAPI;
using Citadel.Core.Windows.Util;
using Citadel.Core.Windows.Util.Net;
using Citadel.Core.Windows.Util.Update;
using Citadel.Core.Windows.WinAPI;
using Citadel.IPC;
using Citadel.IPC.Messages;
using CitadelCore.Logging;
using CitadelCore.Net.Proxy;
using CitadelCore.Windows.Net.Proxy;
using CitadelService.Data.Filtering;
using CitadelService.Data.Models;
using DistillNET;
using Microsoft.Win32;
using murrayju.ProcessExtensions;
using Newtonsoft.Json;
using NLog;
using opennlp.tools.doccat;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Te.Citadel.Util;
using WindowsFirewallHelper;

namespace CitadelService.Services
{
    internal class FilterServiceProvider
    {
        #region Windows Service API

        public bool Start()
        {
            try
            {
                OnStartup();
            }
            catch(Exception e)
            {
                // Critical failure.
                LoggerUtil.RecursivelyLogException(m_logger, e);
                return false;
            }

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
            ReviveGuiForCurrentUser(true);
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

        private CategoryIndex m_categoryIndex = new CategoryIndex(short.MaxValue);

        private IPCServer m_ipcServer;

        /// <summary>
        /// Used for synchronization whenever our NLP model gets updated while we're already initialized. 
        /// </summary>
        private ReaderWriterLockSlim m_doccatSlimLock = new ReaderWriterLockSlim();

        private List<CategoryMappedDocumentCategorizerModel> m_documentClassifiers = new List<CategoryMappedDocumentCategorizerModel>();

        private ProxyServer m_filteringEngine;

        private BackgroundWorker m_filterEngineStartupBgWorker;
        
        private byte[] m_blockedHtmlPage;

        private static readonly DateTime s_Epoch = new DateTime(1970, 1, 1);

        private static readonly string s_EpochHttpDateTime = s_Epoch.ToString("r");

        /// <summary>
        /// Applications we never ever want to filter. Right now, this is just OS binaries. 
        /// </summary>
        private static readonly HashSet<string> s_foreverWhitelistedApplications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Whenever we load filtering rules, we simply make up numbers for categories as we go
        /// along. We use this object to store what strings we map to numbers.
        /// </summary>
        private ConcurrentDictionary<string, MappedFilterListCategoryModel> m_generatedCategoriesMap = new ConcurrentDictionary<string, MappedFilterListCategoryModel>(StringComparer.OrdinalIgnoreCase);

        #endregion FilteringEngineVars

        private ReaderWriterLockSlim m_filteringRwLock = new ReaderWriterLockSlim();

        private ReaderWriterLockSlim m_updateRwLock = new ReaderWriterLockSlim();

        private FilterDbCollection m_filterCollection;

        private BagOfTextTriggers m_textTriggers;

        /// <summary>
        /// Timer used to query for filter list changes every X minutes, as well as application updates. 
        /// </summary>
        private Timer m_updateCheckTimer;

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
        private readonly Logger m_logger;

        /// <summary>
        /// This BackgroundWorker object handles initializing the application off the UI thread.
        /// Allows the splash screen to function.
        /// </summary>
        private BackgroundWorker m_backgroundInitWorker;

        /// <summary>
        /// App function config file. 
        /// </summary>
        private AppConfigModel m_userConfig;

        /// <summary>
        /// We split out this accessor to get all the code here unhooked from directly accessing the
        /// local reference. We do this because reference checks and assignment are guaranteed atomic
        /// on all .NET platforms. So, we used this to wein all the code off of direct access, so
        /// they take a copy to the current reference atomically, and then can use it accordingly
        /// (null checks etc).
        /// </summary>
        private AppConfigModel Config
        {
            get
            {
                return m_userConfig;
            }
        }

        /// <summary>
        /// Json deserialization/serialization settings for our config related data. 
        /// </summary>
        private JsonSerializerSettings m_configSerializerSettings;

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
        /// Stores all, if any, applications that should be forced throught the filter. 
        /// </summary>
        private HashSet<string> m_blacklistedApplications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Stores all, if any, applications that should not be forced through the filter. 
        /// </summary>
        private HashSet<string> m_whitelistedApplications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
        /// This timer is used to monitor local NIC cards and enforce DNS settings when they are
        /// configured in the application config.
        /// </summary>
        private Timer m_dnsEnforcementTimer;

        /// <summary>
        /// Used to ensure synchronized access when setting DNS settings. 
        /// </summary>
        private object m_dnsEnforcementLock = new object();

        private AppcastUpdater m_updater = null;

        private ApplicationUpdate m_lastFetchedUpdate = null;

        private ReaderWriterLockSlim m_appcastUpdaterLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Default ctor. 
        /// </summary>
        public FilterServiceProvider()
        {
            m_logger = LoggerUtil.GetAppWideLogger();

            // Enforce good/proper protocols
            ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & ~SecurityProtocolType.Ssl3) | (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);

            NetworkStatus.Default.ConnectionStateChanged += () =>
            {
                // Bit of a hack here to ensure that we immediately purge
                if(NetworkStatus.Default.BehindIPv4CaptivePortal || NetworkStatus.Default.BehindIPv6CaptivePortal)
                {
                    TryEnfornceDns();
                }
            };
        }

        private void OnStartup()
        {
            // Hook the shutdown/logoff event.
            SystemEvents.SessionEnding += OnAppSessionEnding;

            // Hook app exiting function. This must be done on this main app thread.
            AppDomain.CurrentDomain.ProcessExit += OnApplicationExiting;

            try
            {
                var bitVersionUri = string.Empty;
                if(Environment.Is64BitProcess)
                {
                    bitVersionUri = "/update/winx64/update.xml";
                }
                else
                {
                    bitVersionUri = "/update/winx86/update.xml";
                }

                var appUpdateInfoUrl = string.Format("{0}{1}", WebServiceUtil.Default.ServiceProviderApiPath, bitVersionUri);

                m_updater = new AppcastUpdater(new Uri(appUpdateInfoUrl));
            }
            catch(Exception e)
            {
                // This is a critical error. We cannot recover from this.
                m_logger.Error("Critical error - Could not create application updater.");
                LoggerUtil.RecursivelyLogException(m_logger, e);

                Environment.Exit(-1);
            }

            WebServiceUtil.Default.AuthTokenRejected += () =>
            {
                ReviveGuiForCurrentUser();                
                m_ipcServer.NotifyAuthenticationStatus(Citadel.IPC.Messages.AuthenticationAction.Required);
            };

            try
            {
                m_ipcServer = new IPCServer();

                m_ipcServer.AttemptAuthentication = (args) =>
                {
                    try
                    {
                        if(!string.IsNullOrEmpty(args.Username) && !string.IsNullOrWhiteSpace(args.Username) && args.Password != null && args.Password.Length > 0)
                        {
                            byte[] unencrypedPwordBytes = null;
                            try
                            {
                                unencrypedPwordBytes = args.Password.SecureStringBytes();

                                var authResult = WebServiceUtil.Default.Authenticate(args.Username, unencrypedPwordBytes);

                                switch(authResult)
                                {
                                    case AuthenticationResult.Success:
                                    {
                                        Status = FilterStatus.Running;
                                        m_ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Authenticated);

                                        // Probe server for updates now.
                                        ProbeMasterForApplicationUpdates();
                                        OnUpdateTimerElapsed(null);
                                    }
                                    break;

                                    case AuthenticationResult.Failure:
                                    {
                                        ReviveGuiForCurrentUser();                                        
                                        m_ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Required);
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
                                if(unencrypedPwordBytes != null && unencrypedPwordBytes.Length > 0)
                                {
                                    Array.Clear(unencrypedPwordBytes, 0, unencrypedPwordBytes.Length);
                                    unencrypedPwordBytes = null;
                                }
                            }
                        }
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }
                };

                m_ipcServer.ClientAcceptedPendingUpdate = () =>
                {
                    try
                    {
                        m_appcastUpdaterLock.EnterWriteLock();

                        if(m_lastFetchedUpdate != null)
                        {
                            m_lastFetchedUpdate.DownloadUpdate().Wait();

                            m_ipcServer.NotifyUpdating();
                            m_lastFetchedUpdate.BeginInstallUpdateDelayed();
                            Task.Delay(200).Wait();

                            m_logger.Info("Shutting down to update.");

                            if(m_appcastUpdaterLock.IsWriteLockHeld)
                            {
                                m_appcastUpdaterLock.ExitWriteLock();
                            }

                            Environment.Exit((int)ExitCodes.ShutdownForUpdate);
                        }
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }
                    finally
                    {
                        if(m_appcastUpdaterLock.IsWriteLockHeld)
                        {
                            m_appcastUpdaterLock.ExitWriteLock();
                        }
                    }
                };

                m_ipcServer.DeactivationRequested = (args) =>
                {
                    Status = FilterStatus.Synchronizing;

                    try
                    {
                        HttpStatusCode responseCode;
                        var response = WebServiceUtil.Default.RequestResource(ServiceResource.DeactivationRequest, out responseCode);

                        args.Granted = responseCode == HttpStatusCode.OK || responseCode == HttpStatusCode.NoContent;

                        if(args.Granted)
                        {
                            Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                        }
                        else
                        {
                            Status = FilterStatus.Running;
                        }
                    }
                    catch(Exception e)
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
                    switch(args.Command)
                    {
                        case RelaxedPolicyCommand.Relinquished:
                        {
                            OnRelinquishRelaxedPolicyRequested();
                        }
                        break;

                        case RelaxedPolicyCommand.Requested:
                        {
                            OnRelaxedPolicyRequested();
                        }
                        break;
                    }
                };

                m_ipcServer.ClientRequestsBlockActionReview += (NotifyBlockActionMessage blockActionMsg) =>
                {
                    var curAuthToken = WebServiceUtil.Default.AuthToken;

                    if(curAuthToken != null && curAuthToken.Length > 0)
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

                            //m_logger.Info("Starting process: {0}", AppAssociationHelper.PathToDefaultBrowser);
                            //m_logger.Info("With args: {0}", reportPath);

                            var sanitizedArgs = "\"" + Regex.Replace(reportPath, @"(\\+)$", @"$1$1") + "\"";
                            var sanitizedPath = "\"" + Regex.Replace(AppAssociationHelper.PathToDefaultBrowser, @"(\\+)$", @"$1$1") + "\"" + " " + sanitizedArgs;
                            ProcessExtensions.StartProcessAsCurrentUser(null, sanitizedPath);

                            //var cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                            //ProcessExtensions.StartProcessAsCurrentUser(cmdPath, string.Format("/c start \"{0}\"", reportPath));
                        }
                        catch(Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }
                };

                m_ipcServer.ClientConnected = () =>
                {
                    ConnectedClients++;

                    // When a client connects, synchronize our data. Presently, we just want to
                    // update them with relaxed policy NFO, if any.
                    var cfg = Config;
                    if(cfg != null && cfg.BypassesPermitted > 0)
                    {
                        m_ipcServer.NotifyRelaxedPolicyChange(cfg.BypassesPermitted - cfg.BypassesUsed, cfg.BypassDuration);
                    }
                    else
                    {
                        m_ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero);
                    }

                    m_ipcServer.NotifyStatus(Status);

                    if(m_ipcServer.WaitingForAuth)
                    {   
                        m_ipcServer.NotifyAuthenticationStatus(AuthenticationAction.Required);
                    }
                };

                m_ipcServer.ClientDisconnected = () =>
                {   
                    ConnectedClients--;

                    // All GUI clients are gone and no one logged in. Shut it down.
                    if(ConnectedClients <= 0 && m_ipcServer.WaitingForAuth)
                    {
                        Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                    }
                };
            }
            catch(Exception ipce)
            {
                // This is a critical error. We cannot recover from this.
                m_logger.Error("Critical error - Could not start IPC server.");
                LoggerUtil.RecursivelyLogException(m_logger, ipce);

                Environment.Exit(-1);
            }

            // Before we do any network stuff, ensure we have windows firewall access.
            EnsureWindowsFirewallAccess();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Run the background init worker for non-UI related initialization.
            m_backgroundInitWorker = new BackgroundWorker();
            m_backgroundInitWorker.DoWork += DoBackgroundInit;
            m_backgroundInitWorker.RunWorkerCompleted += OnBackgroundInitComplete;

            m_backgroundInitWorker.RunWorkerAsync();
        }

        private void EnsureWindowsFirewallAccess()
        {
            try
            {
                string thisProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                var thisAssembly = System.Reflection.Assembly.GetExecutingAssembly();

                // Get all existing rules matching our process name and destroy them.
                var myRules = FirewallManager.Instance.Rules.Where(r => r.Name.Equals(thisProcessName, StringComparison.OrdinalIgnoreCase)).ToArray();
                if(myRules != null && myRules.Length > 0)
                {
                    foreach(var rule in myRules)
                    {
                        FirewallManager.Instance.Rules.Remove(rule);
                    }
                }

                // Create inbound/outbound firewall rules and add them.
                var inboundRule = FirewallManager.Instance.CreateApplicationRule(
                    FirewallProfiles.Domain | FirewallProfiles.Private | FirewallProfiles.Public,
                    thisProcessName,
                    FirewallAction.Allow, thisAssembly.Location
                );
                inboundRule.Direction = FirewallDirection.Inbound;

                FirewallManager.Instance.Rules.Add(inboundRule);

                var outboundRule = FirewallManager.Instance.CreateApplicationRule(
                    FirewallProfiles.Domain | FirewallProfiles.Private | FirewallProfiles.Public,
                    thisProcessName,
                    FirewallAction.Allow, thisAssembly.Location
                );
                outboundRule.Direction = FirewallDirection.Outbound;

                FirewallManager.Instance.Rules.Add(outboundRule);
            }
            catch(Exception e)
            {
                m_logger.Error("Error while attempting to configure firewall application exception.");
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        private void OnAppSessionEnding(object sender, SessionEndingEventArgs e)
        {
            m_logger.Info("Session ending.");

            // THIS MUST BE DONE HERE ALWAYS, otherwise, we get BSOD.
            CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();

            Environment.Exit((int)ExitCodes.ShutdownWithSafeguards);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception err = e.ExceptionObject as Exception;
            LoggerUtil.RecursivelyLogException(m_logger, err);
        }

        /// <summary>
        /// Called only in circumstances where the application config requires use of the block
        /// action threshold tracking functionality.
        /// </summary>
        private void InitThresholdData()
        {
            // If exists, stop it first.
            if(m_thresholdCountTimer != null)
            {
                m_thresholdCountTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }

            // Create the threshold count timer and start it with the configured timespan.
            var cfg = Config;
            m_thresholdCountTimer = new Timer(OnThresholdTriggerPeriodElapsed, null, cfg != null ? cfg.ThresholdTriggerPeriod : TimeSpan.FromMinutes(1), Timeout.InfiniteTimeSpan);

            // Create the enforcement timer, but don't start it.
            m_thresholdEnforcementTimer = new Timer(OnThresholdTimeoutPeriodElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Downloads, if necessary and able, a fresh copy of the filtering data for this user. 
        /// </summary>
        /// <returns>
        /// True if new list data was downloaded, false otherwise. 
        /// </returns>
        private bool UpdateListData()
        {
            HttpStatusCode code;
            var rHashBytes = WebServiceUtil.Default.RequestResource(ServiceResource.UserDataSumCheck, out code);

            var listDataFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "a.dat");

            if(code == HttpStatusCode.OK && rHashBytes != null)
            {
                // Notify all clients that we just successfully made contact with the server.
                // We don't set the status here, because we'd have to store it and set it
                // back, so we just directly issue this msg.
                m_ipcServer.NotifyStatus(FilterStatus.Synchronized);

                var rhash = Encoding.UTF8.GetString(rHashBytes);

                bool needsUpdate = false;

                if(!File.Exists(listDataFilePath) || new FileInfo(listDataFilePath).Length == 0)
                {
                    needsUpdate = true;
                }
                else
                {
                    // We're going to hash our local version and compare. If they don't match, we're
                    // going to update our lists.

                    using(var fs = File.OpenRead(listDataFilePath))
                    {
                        using(SHA1 sec = new SHA1CryptoServiceProvider())
                        {
                            byte[] bt = sec.ComputeHash(fs);
                            var lHash = BitConverter.ToString(bt).Replace("-", "");

                            if(!lHash.OIEquals(rhash))
                            {
                                needsUpdate = true;
                            }
                        }
                    }
                }

                if(!needsUpdate)
                {
                    // We got a response from our server. We have the right data. Just check and see
                    // if we don't have a current user CFG. If we don't, then return true to force
                    // a reload of the config and list data from the local FS.
                    var cfg = Config;
                    if(cfg == null && File.Exists(listDataFilePath) && new FileInfo(listDataFilePath).Length >= 0)
                    {
                        return true;
                    }
                }

                if(needsUpdate)
                {
                    m_logger.Info("Updating filtering rules, rules missing or integrity violation.");                    
                    var filterDataZipBytes = WebServiceUtil.Default.RequestResource(ServiceResource.UserDataRequest, out code);
                    
                    if(code == HttpStatusCode.OK && filterDataZipBytes != null && filterDataZipBytes.Length > 0)
                    {
                        File.WriteAllBytes(listDataFilePath, filterDataZipBytes);
                    }
                    else
                    {
                        Debug.WriteLine("Failed to download list data.");
                        m_logger.Error("Failed to download list data.");
                    }
                }

                return needsUpdate;
            }
            else
            {

                // We didn't get any response from our server. Check if the list exists
                // and is healthy, and make sure it's loaded ONLY if we don't already have
                // a CFG loaded (meaning we have yet to load any data at all).
                var cfg = Config;
                if(cfg == null && File.Exists(listDataFilePath) && new FileInfo(listDataFilePath).Length >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void ProbeMasterForApplicationUpdates()
        {
            bool hadError = false;

            try
            {
                m_appcastUpdaterLock.EnterWriteLock();

                m_lastFetchedUpdate = m_updater.CheckForUpdate(m_userConfig != null ? m_userConfig.UpdateChannel : string.Empty).Result;

                if(m_lastFetchedUpdate != null)
                {
                    m_logger.Info("Found update. Asking clients to accept update.");

                    ReviveGuiForCurrentUser();

                    Task.Delay(500).Wait();

                    m_ipcServer.NotifyApplicationUpdateAvailable(new ServerUpdateQueryMessage(m_lastFetchedUpdate.Title, m_lastFetchedUpdate.HtmlBody, m_lastFetchedUpdate.CurrentVersion.ToString(), m_lastFetchedUpdate.UpdateVersion.ToString()));
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
                hadError = true;
            }
            finally
            {
                m_appcastUpdaterLock.ExitWriteLock();
            }

            if(!hadError)
            {
                // Notify all clients that we just successfully made contact with the server.
                // We don't set the status here, because we'd have to store it and set it
                // back, so we just directly issue this msg.
                m_ipcServer.NotifyStatus(FilterStatus.Synchronized);
            }
        }

        /// <summary>
        /// Sets up the filtering engine, gets discovered installations of firefox to trust the
        /// engine, sets up callbacks for classification and firewall checks, but does not start the engine.
        /// </summary>
        private void InitEngine()
        {
            // Get our CA-Bundle resource and unpack it to the application directory.
            var caCertPackURI = "CitadelService.Resources.ca-cert.pem";
            StringBuilder caFileBuilder = new StringBuilder();
            using(var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(caCertPackURI))
            {
                if(resourceStream != null && resourceStream.CanRead)
                {
                    using(TextReader tsr = new StreamReader(resourceStream))
                    {
                        caFileBuilder = new StringBuilder(tsr.ReadToEnd());
                    }
                }
                else
                {
                    m_logger.Error("Cannot read from packed ca bundle resource.");
                }
            }

            caFileBuilder.AppendLine();

            // Get our blocked HTML page
            var blockedPagePackURI = "CitadelService.Resources.BlockedPage.html";
            using(var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(blockedPagePackURI))
            {
                if(resourceStream != null && resourceStream.CanRead)
                {
                    using(TextReader tsr = new StreamReader(resourceStream))
                    {
                        m_blockedHtmlPage = Encoding.UTF8.GetBytes(tsr.ReadToEnd());
                    }
                }
                else
                {
                    m_logger.Error("Cannot read from packed block page file.");
                }
            }

            m_filterCollection = new FilterDbCollection(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rules.db"), true, true);

            m_textTriggers = new BagOfTextTriggers(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "t.dat"), true, true);

            // Get Microsoft root authorities. We need this in order to permit Windows Update and
            // such in the event that it is forced through the filter.
            var toTrust = new List<StoreName>() {
                StoreName.Root,
                StoreName.AuthRoot,
                StoreName.CertificateAuthority,
                StoreName.TrustedPublisher,
                StoreName.TrustedPeople
            };

            foreach(var trust in toTrust)
            {
                using(X509Store localStore = new X509Store(trust, StoreLocation.LocalMachine))
                {
                    localStore.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
                    foreach(var cert in localStore.Certificates)
                    {
                        caFileBuilder.AppendLine(cert.ExportToPem());
                    }
                    localStore.Close();
                }
            }

            // Dump the text to the local file system.
            var localCaBundleCertPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ca-cert.pem");
            File.WriteAllText(localCaBundleCertPath, caFileBuilder.ToString());

            // Init the engine with our callbacks, the path to the ca-bundle, let it pick whatever
            // ports it wants for listening, and give it our total processor count on this machine as
            // a hint for how many threads to use.
            m_filteringEngine = new WindowsProxyServer(OnAppFirewallCheck, OnHttpMessageBegin, OnHttpMessageEnd);

            // Setup general info, warning and error events.
            LoggerProxy.Default.OnInfo += EngineOnInfo;
            LoggerProxy.Default.OnWarning += EngineOnWarning;
            LoggerProxy.Default.OnError += EngineOnError;

            // Start filtering, always.
            if(m_filteringEngine != null && !m_filteringEngine.IsRunning)
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
                EstablishTrustWithFirefox();
            }
            catch(Exception ffe)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ffe);
            }
        }

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
            // Setup json serialization settings.
            m_configSerializerSettings = new JsonSerializerSettings();
            m_configSerializerSettings.NullValueHandling = NullValueHandling.Ignore;

            // Init the Engine in the background.
            try
            {
                InitEngine();
            }
            catch(Exception ie)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ie);
            }

            // Force start our cascade of protective processes.
            try
            {
                ServiceSpawner.Instance.InitializeServices();
            }
            catch(Exception se)
            {
                LoggerUtil.RecursivelyLogException(m_logger, se);
            }

            // Init update timer.
            m_updateCheckTimer = new Timer(OnUpdateTimerElapsed, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
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
            catch(Exception err)
            {
                LoggerUtil.RecursivelyLogException(m_logger, err);
            }

            try
            {
                if(Environment.ExitCode == (int)ExitCodes.ShutdownWithoutSafeguards)
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
            catch(Exception err)
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
            WFPUtility.EnableInternet();

            if(e.Cancelled || e.Error != null)
            {
                m_logger.Error("Error during initialization.");
                if(e.Error != null && m_logger != null)
                {
                    LoggerUtil.RecursivelyLogException(m_logger, e.Error);
                }

                Environment.Exit(-1);
                return;
            }
            
            OnUpdateTimerElapsed(null);

            Status = FilterStatus.Running;

            ReviveGuiForCurrentUser(true);
        }

        /// <summary>
        /// Searches for FireFox installations and enables trust of the local certificate store. 
        /// </summary>
        /// <remarks>
        /// If any profile is discovered that does not have the local CA cert store checking enabled
        /// already, all instances of firefox will be killed and then restarted when calling this method.
        /// </remarks>
        private void EstablishTrustWithFirefox()
        {
            // This path will be DRIVE:\USER_PATH\Public\Desktop
            var usersBasePath = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
            usersBasePath = usersBasePath.Parent;
            usersBasePath = usersBasePath.Parent;

            var ffProfileDirs = new List<string>();

            var userDirs = Directory.GetDirectories(usersBasePath.FullName);

            foreach(var userDir in userDirs)
            {
                if(Directory.Exists(Path.Combine(userDir, @"AppData\Roaming\Mozilla\Firefox\Profiles")))
                {
                    ffProfileDirs.Add(Path.Combine(userDir, @"AppData\Roaming\Mozilla\Firefox\Profiles"));
                }
            }

            if(ffProfileDirs.Count <= 0)
            {
                return;
            }

            var valuesThatNeedToBeSet = new Dictionary<string, string>();

            var firefoxUserCfgValuesUri = "CitadelService.Resources.FireFoxUserCFG.txt";
            using(var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(firefoxUserCfgValuesUri))
            {
                if(resourceStream != null && resourceStream.CanRead)
                {
                    using(TextReader tsr = new StreamReader(resourceStream))
                    {
                        string cfgLine = null;
                        while((cfgLine = tsr.ReadLine()) != null)
                        {
                            if(cfgLine.Length > 0)
                            {
                                var firstSpace = cfgLine.IndexOf(' ');

                                if(firstSpace != -1)
                                {
                                    var key = cfgLine.Substring(0, firstSpace);
                                    var value = cfgLine.Substring(firstSpace);

                                    if(!valuesThatNeedToBeSet.ContainsKey(key))
                                    {
                                        valuesThatNeedToBeSet.Add(key, value);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    m_logger.Error("Cannot read from firefox cfg resource file.");
                }
            }

            foreach(var ffProfDir in ffProfileDirs)
            {
                var prefsFiles = Directory.GetFiles(ffProfDir, "prefs.js", SearchOption.AllDirectories);

                foreach(var prefFile in prefsFiles)
                {
                    var userFile = Path.Combine(Path.GetDirectoryName(prefFile), "user.js");

                    string[] fileText = new string[0];

                    if(File.Exists(userFile))
                    {
                        fileText = File.ReadAllLines(prefFile);
                    }

                    var notFound = new Dictionary<string, string>();

                    foreach(var kvp in valuesThatNeedToBeSet)
                    {
                        var entryIndex = Array.FindIndex(fileText, l => l.StartsWith(kvp.Key));

                        if(entryIndex != -1)
                        {
                            if(!fileText[entryIndex].EndsWith(kvp.Value))
                            {
                                fileText[entryIndex] = kvp.Key + kvp.Value;
                                m_logger.Info("Firefox profile {0} has has preference {1}) adjusted to be set correctly already.", Directory.GetParent(prefFile).Name, kvp.Key);
                            }
                            else
                            {
                                m_logger.Info("Firefox profile {0} has preference {1}) set correctly already.", Directory.GetParent(prefFile).Name, kvp.Key);
                            }
                        }
                        else
                        {
                            notFound.Add(kvp.Key, kvp.Value);
                        }
                    }

                    var fileTextList = new List<string>(fileText);

                    foreach(var nfk in notFound)
                    {
                        m_logger.Info("Firefox profile {0} is having preference {1}) added.", Directory.GetParent(prefFile).Name, nfk.Key);
                        fileTextList.Add(nfk.Key + nfk.Value);
                    }

                    File.WriteAllLines(userFile, fileTextList);
                }
            }

            // Figure out if firefox is running. If later it is and we kill it, store the path to
            // firefox.exe so we can restart the process after we're done.
            string firefoxExePath = string.Empty;
            bool firefoxIsRunning = Process.GetProcessesByName("firefox").Length > 0;

            // Always kill firefox.
            if(firefoxIsRunning)
            {
                // We need to kill firefox before editing the preferences, otherwise they'll just get overwritten.
                foreach(var ff in Process.GetProcessesByName("firefox"))
                {
                    firefoxExePath = ff.MainModule.FileName;

                    try
                    {
                        ff.Kill();
                        ff.Dispose();
                    }
                    catch { }
                }
            }

            // Means we force closed at least once instance of firefox. Relaunch it now to cause it
            // to run restore.
            if(firefoxIsRunning && StringExtensions.Valid(firefoxExePath))
            {
                // Start the process and abandon our handle.
                ProcessExtensions.StartProcessAsCurrentUser(firefoxExePath);
            }
        }

        #region EngineCallbacks

        private void EngineOnInfo(string message)
        {
            m_logger.Info(message);
        }

        private void EngineOnWarning(string message)
        {
            m_logger.Warn(message);
        }

        private void EngineOnError(string message)
        {
            m_logger.Error(message);
        }

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

            var cfg = Config;

            if(cfg != null && cfg.UseThreshold)
            {
                var currentTicks = Interlocked.Increment(ref m_thresholdTicks);

                if(currentTicks >= cfg.ThresholdLimit)
                {
                    internetShutOff = true;

                    try
                    {
                        m_logger.Warn("Block action threshold met or exceeded. Disabling internet.");
                        WFPUtility.DisableInternet();
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }

                    this.m_thresholdEnforcementTimer.Change(cfg.ThresholdTimeoutPeriod, Timeout.InfiniteTimeSpan);
                }
            }

            string categoryNameString = "Unknown";
            var mappedCategory = m_generatedCategoriesMap.Values.Where(xx => xx.CategoryId == category).FirstOrDefault();

            if(mappedCategory != null)
            {
                categoryNameString = mappedCategory.CategoryName;
            }

            m_ipcServer.NotifyBlockAction(cause, requestUri, categoryNameString, matchingRule);

            if(internetShutOff)
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
        /// Called whenever the Engine want's to check if the application at the supplied absolute
        /// path should have its traffic forced through itself or not.
        /// </summary>
        /// <param name="appAbsolutePath">
        /// The absolute path to an application that the filter is inquiring about. 
        /// </param>
        /// <returns>
        /// True if the application at the specified absolute path should have its traffic forced
        /// through the filtering engine, false otherwise.
        /// </returns>
        private bool OnAppFirewallCheck(string appAbsolutePath)
        {   
            // XXX TODO - The engine shouldn't even tell us about SYSTEM processes and just silently
            // let them through.
            if(appAbsolutePath.OIEquals("SYSTEM"))
            {
                return false;
            }

            // Lets completely avoid piping anything from the operating system in the filter, with
            // the sole exception of Microsoft edge.
            if((appAbsolutePath.IndexOf("MicrosoftEdge", StringComparison.OrdinalIgnoreCase) == -1) && appAbsolutePath.IndexOf(@"\Windows\", StringComparison.OrdinalIgnoreCase) != -1)
            {
                lock(s_foreverWhitelistedApplications)
                {
                    if(s_foreverWhitelistedApplications.Contains(appAbsolutePath))
                    {
                        return false;
                    }
                }

                // Here we'll simply check if the binary is signed. If so, we'll validate the
                // certificate. If the cert is good, let's just go and bypass this binary altogether.
                // However, note that this does not verify that the signed binary is actually valid
                // for the certificate. That is, it doesn't ensure file integrity. Also, note that
                // even if we went all the way as to use WinVerifyTrust() from wintrust.dll to
                // completely verify integrity etc, this can still be bypassed by adding a self
                // signed signing authority to the windows trusted certs.
                //
                // So, all we can do is kick the can further down the road. This should be sufficient
                // to prevent the lay person from dropping a browser into the Windows folder.
                //
                // Leaving above notes just for the sake of knowledge. We can kick the can pretty
                // darn far down the road by asking Windows Resource Protection if the file really
                // belongs to the OS. Viruses are known to call SfcIsFileProtected in order to avoid
                // getting caught messing with these files so if viruses avoid them, I think we've
                // booted the can so far down the road that we need not worry about being exploited
                // here. The OS would need to be funamentally compromised and that wouldn't be our fault.
                //
                // The only other way we could get exploited here by getting our hook to sfc.dll
                // hijacked. There are countermeasures of course but not right now.

                // If the result is greater than zero, then this is a protected operating system file
                // according to the operating system.
                if(SFC.SfcIsFileProtected(IntPtr.Zero, appAbsolutePath) > 0)
                {
                    lock(s_foreverWhitelistedApplications)
                    {
                        s_foreverWhitelistedApplications.Add(appAbsolutePath);
                    }

                    return false;
                }
            }

            try
            {
                m_filteringRwLock.EnterReadLock();

                if(m_blacklistedApplications.Count == 0 && m_whitelistedApplications.Count == 0)
                {
                    // Just filter anything accessing port 80 and 443.
                    m_logger.Debug("1Filtering application: {0}", appAbsolutePath);
                    return true;
                }

                var appName = Path.GetFileName(appAbsolutePath);

                if(m_whitelistedApplications.Count > 0)
                {
                    if(m_whitelistedApplications.Contains(appName))
                    {
                        // Whitelist is in effect and this app is whitelisted. So, don't force it through.
                        return false;
                    }

                    // Whitelist is in effect, and this app is not whitelisted, so force it through.
                    m_logger.Debug("2Filtering application: {0}", appAbsolutePath);
                    return true;
                }

                if(m_blacklistedApplications.Count > 0)
                {
                    if(m_blacklistedApplications.Contains(appName))
                    {
                        // Blacklist is in effect and this app is blacklisted. So, force it through.
                        m_logger.Debug("3Filtering application: {0}", appAbsolutePath);
                        return true;
                    }

                    // Blacklist in effect but this app is not on the blacklist. Don't force it through.
                    return false;
                }

                // This app was not hit by either an enforced whitelist or blacklist. So, by default
                // we will filter everything. We should never get here, but just in case.

                m_logger.Debug("4Filtering application: {0}", appAbsolutePath);
                return true;
            }
            catch(Exception e)
            {
                m_logger.Error("Error in {0}", nameof(OnAppFirewallCheck));
                LoggerUtil.RecursivelyLogException(m_logger, e);
                return false;
            }
            finally
            {
                m_filteringRwLock.ExitReadLock();
            }
        }

        private void OnHttpMessageBegin(Uri requestUrl, string headers, byte[] body, MessageType msgType, MessageDirection msgDirection, out ProxyNextAction nextAction, out string customBlockResponseContentType, out byte[] customBlockResponse)
        {
            nextAction = ProxyNextAction.AllowAndIgnoreContent;
            customBlockResponseContentType = null;
            customBlockResponse = null;

            // Don't allow filtering if our user has been denied access and they
            // have not logged back in.
            if(m_ipcServer != null && m_ipcServer.WaitingForAuth)
            {
                return;
            }

            bool readLocked = false;

            try
            {                
                var parsedHeaders = ParseHeaders(headers);

                if(m_filterCollection != null)
                {
                    readLocked = true;
                    m_filteringRwLock.EnterReadLock();

                    var filters = m_filterCollection.GetWhitelistFiltersForDomain(requestUrl.Host).Result;
                    short matchCategory = -1;
                    UrlFilter matchingFilter = null;

                    if(CheckIfFiltersApply(filters, requestUrl, parsedHeaders, out matchingFilter, out matchCategory))
                    {
                        var mappedCategory = m_generatedCategoriesMap.Values.Where(xx => xx.CategoryId == matchCategory).FirstOrDefault();

                        if(mappedCategory != null)
                        {
                            m_logger.Info("Request {0} whitelisted by rule {1} in category {2}.", requestUrl.ToString(), matchingFilter.OriginalRule, mappedCategory.CategoryName);
                        }
                        else
                        {
                            m_logger.Info("Request {0} whitelisted by rule {1} in category {2}.", requestUrl.ToString(), matchingFilter.OriginalRule, matchCategory);
                        }

                        nextAction = ProxyNextAction.AllowAndIgnoreContentAndResponse;
                        return;
                    }

                    filters = m_filterCollection.GetWhitelistFiltersForDomain().Result;

                    if(CheckIfFiltersApply(filters, requestUrl, parsedHeaders, out matchingFilter, out matchCategory))
                    {
                        var mappedCategory = m_generatedCategoriesMap.Values.Where(xx => xx.CategoryId == matchCategory).FirstOrDefault();

                        if(mappedCategory != null)
                        {
                            m_logger.Info("Request {0} whitelisted by rule {1} in category {2}.", requestUrl.ToString(), matchingFilter.OriginalRule, mappedCategory.CategoryName);
                        }
                        else
                        {
                            m_logger.Info("Request {0} whitelisted by rule {1} in category {2}.", requestUrl.ToString(), matchingFilter.OriginalRule, matchCategory);
                        }

                        nextAction = ProxyNextAction.AllowAndIgnoreContentAndResponse;
                        return;
                    }

                    filters = m_filterCollection.GetFiltersForDomain(requestUrl.Host).Result;

                    if(CheckIfFiltersApply(filters, requestUrl, parsedHeaders, out matchingFilter, out matchCategory))
                    {
                        OnRequestBlocked(matchCategory, BlockType.Url, requestUrl, matchingFilter.OriginalRule);
                        nextAction = ProxyNextAction.DropConnection;
                        customBlockResponseContentType = "text/html";
                        customBlockResponse = m_blockedHtmlPage;
                        return;
                    }

                    filters = m_filterCollection.GetFiltersForDomain().Result;

                    if(CheckIfFiltersApply(filters, requestUrl, parsedHeaders, out matchingFilter, out matchCategory))
                    {
                        OnRequestBlocked(matchCategory, BlockType.Url, requestUrl, matchingFilter.OriginalRule);
                        nextAction = ProxyNextAction.DropConnection;
                        customBlockResponseContentType = "text/html";
                        customBlockResponse = m_blockedHtmlPage;
                        return;
                    }
                }

                string contentType = null;
                if((contentType = parsedHeaders["Content-Type"]) != null)
                {
                    m_logger.Info("Got content type {0} in {1}", contentType, nameof(OnHttpMessageBegin));

                    // This is the start of a response with a content type that we want to inspect.
                    // Flag it for inspection once done. It will later call the OnHttpMessageEnd callback.
                    var isHtml = contentType.IndexOf("html") != -1;
                    var isJson = contentType.IndexOf("json") != -1;
                    if(isHtml || isJson)
                    {
                        nextAction = ProxyNextAction.AllowButRequestContentInspection;                        
                        return;
                    }
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                if(readLocked)
                {
                    m_filteringRwLock.ExitReadLock();
                }
            }
        }

        
        private void OnHttpMessageEnd(Uri requestUrl, string headers, byte[] body, MessageType msgType, MessageDirection msgDirection, out bool shouldBlock, out string customBlockResponseContentType, out byte[] customBlockResponse)
        {
            shouldBlock = false;
            customBlockResponseContentType = null;
            customBlockResponse = null;

            // Don't allow filtering if our user has been denied access and they
            // have not logged back in.
            if(m_ipcServer != null && m_ipcServer.WaitingForAuth)
            {
                return;
            }

            m_logger.Info("Entering {0}", nameof(OnHttpMessageEnd));

            // The only thing we can really do in this callback, and the only thing we care to do, is
            // try to classify the content of the response payload, if there is any.
            try
            {
                var parsedHeaders = ParseHeaders(headers);

                Uri requestUri = null;

                if(!parsedHeaders.TryGetRequestUri(out requestUri))
                {
                    // Don't bother logging this. This is just google chrome being stupid with URI's for new tabs.
                    //m_logger.Error("Malformed headers in OnHttpMessageBegin. Missing request URI.");
                    return;
                }

                string contentType = null;
                string httpVersion = null;

                if((contentType = parsedHeaders["Content-Type"]) != null)
                {
                    contentType = contentType.ToLower();

                    foreach(string key in parsedHeaders)
                    {
                        string val = parsedHeaders[key];
                        if(val == null || val == string.Empty)
                        {
                            var si = key.IndexOf(' ');
                            if(si != -1)
                            {
                                var sub = key.Substring(0, si);

                                if(sub.StartsWith("HTTP/"))
                                {
                                    httpVersion = sub.Substring(5);
                                    break;
                                }
                            }
                        }
                    }

                    httpVersion = httpVersion != null ? httpVersion : "1.1";

                    BlockType blockType;
                    var contentClassResult = OnClassifyContent(body, contentType, out blockType);

                    if(contentClassResult > 0)
                    {
                        shouldBlock = true;

                        if(contentType.IndexOf("html") != -1)
                        {
                            customBlockResponseContentType = "text/html";
                            customBlockResponse = m_blockedHtmlPage;
                        }
                        
                        OnRequestBlocked(contentClassResult, blockType, requestUri);
                        m_logger.Info("Response blocked by content classification.");
                    }
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        private bool CheckIfFiltersApply(List<UrlFilter> filters, Uri request, NameValueCollection headers, out UrlFilter matched, out short matchedCategory)
        {
            matchedCategory = -1;
            matched = null;

            var len = filters.Count;
            for(int i = 0; i < len; ++i)
            {
                Console.WriteLine(filters[i].IsException);
                if(m_categoryIndex.GetIsCategoryEnabled(filters[i].CategoryId) && filters[i].IsMatch(request, headers))
                {
                    matched = filters[i];
                    matchedCategory = filters[i].CategoryId;
                    return true;
                }
            }

            return false;
        }

        private NameValueCollection ParseHeaders(string headers)
        {
            var nvc = new NameValueCollection(StringComparer.OrdinalIgnoreCase);

            using(var reader = new StringReader(headers))
            {
                string line = null;
                while((line = reader.ReadLine()) != null)
                {
                    if(string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var firstSplitIndex = line.IndexOf(':');
                    if(firstSplitIndex == -1)
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
        private short OnClassifyContent(byte[] data, string contentType, out BlockType blockedBecause)
        {
            try
            {
                m_filteringRwLock.EnterReadLock();

                if(m_textTriggers != null && m_textTriggers.HasTriggers)
                {
                    var isHtml = contentType.IndexOf("html") != -1;
                    var isJson = contentType.IndexOf("json") != -1;
                    if(isHtml || isJson)
                    {
                        var dataToAnalyzeStr = Encoding.UTF8.GetString(data);

                        if(isHtml)
                        {
                            var ext = new FastHtmlTextExtractor();
                            dataToAnalyzeStr = ext.Extract(dataToAnalyzeStr.ToCharArray(), true);
                        }

                        short matchedCategory = -1;
                        string trigger = null;
                        var cfg = Config;
                        if(m_textTriggers.ContainsTrigger(dataToAnalyzeStr, out matchedCategory, out trigger, m_categoryIndex.GetIsCategoryEnabled, isHtml, cfg != null ? cfg.MaxTextTriggerScanningSize : -1))
                        {
                            var mappedCategory = m_generatedCategoriesMap.Values.Where(xx => xx.CategoryId == matchedCategory).FirstOrDefault();

                            if(mappedCategory != null)
                            {
                                m_logger.Info("Response blocked by text trigger \"{0}\" in category {1}.", trigger, mappedCategory.CategoryName);
                                blockedBecause = BlockType.TextTrigger;
                                return mappedCategory.CategoryId;
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
                m_filteringRwLock.ExitReadLock();
            }

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
                                    var cfg = Config;
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

            // Default to zero. Means don't block this content.
            blockedBecause = BlockType.OtherContentClassification;
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
            // Reset count to zero.
            Interlocked.Exchange(ref m_thresholdTicks, 0);

            var cfg = Config;

            this.m_thresholdCountTimer.Change(cfg != null ? cfg.ThresholdTriggerPeriod : TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
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
                WFPUtility.EnableInternet();
            }
            catch(Exception e)
            {
                m_logger.Warn("Error when trying to reinstate internet on threshold timeout period elapsed.");
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }

            Status = FilterStatus.Running;

            // Disable the timer before we leave.
            this.m_thresholdEnforcementTimer.Change(Timeout.Infinite, Timeout.Infinite);
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
            if(m_ipcServer != null && m_ipcServer.WaitingForAuth)
            {
                return;
            }

            try
            {
                m_logger.Info("Checking for filter list updates.");

                m_updateRwLock.EnterWriteLock();

                bool gotUpdatedFilterLists = UpdateListData();

                if(gotUpdatedFilterLists)
                {
                    // Got new data. Gotta reload.
                    ReloadFilteringRules();
                }

                m_logger.Info("Checking for application updates.");

                // Check for app updates.
                ProbeMasterForApplicationUpdates();
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                // Enable the timer again.
                if(!(NetworkStatus.Default.HasIpv4InetConnection || NetworkStatus.Default.HasIpv6InetConnection))
                {
                    // If we have no internet, keep polling every 15 seconds. We need that data ASAP.
                    this.m_updateCheckTimer.Change(TimeSpan.FromSeconds(15), Timeout.InfiniteTimeSpan);
                }
                else
                {
                    var cfg = Config;
                    if(cfg != null)
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
                WFPUtility.EnableInternet();
            }
            catch { }

            try
            {
                if(m_filteringEngine != null && !m_filteringEngine.IsRunning)
                {
                    m_logger.Info("Start engine.");

                    // Start the engine right away, to ensure the atomic bool IsRunning is set.
                    m_filteringEngine.Start();
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        /// <summary>
        /// Queries the service provider for updated filtering rules. 
        /// </summary>
        private void ReloadFilteringRules()
        {
            try
            {
                m_filteringRwLock.EnterWriteLock();

                // Load our configuration file and load configured lists, etc.
                var configDataFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "a.dat");

                if(File.Exists(configDataFilePath))
                {
                    using(var file = File.OpenRead(configDataFilePath))
                    {
                        using(var zip = new ZipArchive(file, ZipArchiveMode.Read))
                        {
                            // Find the configuration JSON file.
                            string cfgJson = string.Empty;
                            foreach(var entry in zip.Entries)
                            {
                                if(entry.Name.OIEquals("cfg.json"))
                                {
                                    using(var cfgStream = entry.Open())
                                    using(TextReader tr = new StreamReader(cfgStream))
                                    {
                                        cfgJson = tr.ReadToEnd();
                                        break;
                                    }
                                }
                            }

                            if(!StringExtensions.Valid(cfgJson))
                            {
                                m_logger.Error("Could not find valid JSON config for filter.");
                                return;
                            }

                            // Deserialize config
                            try
                            {
                                m_userConfig = JsonConvert.DeserializeObject<AppConfigModel>(cfgJson, m_configSerializerSettings);
                            }
                            catch(Exception deserializationError)
                            {
                                m_logger.Error("Failed to deserialize JSON config.");
                                LoggerUtil.RecursivelyLogException(m_logger, deserializationError);
                                return;
                            }

                            if(m_userConfig.UpdateFrequency.Minutes <= 0 || m_userConfig.UpdateFrequency == Timeout.InfiniteTimeSpan)
                            {
                                // Just to ensure that we enforce a minimum value here.
                                m_userConfig.UpdateFrequency = TimeSpan.FromMinutes(5);
                            }

                            // Enforce DNS if present.
                            TryEnfornceDns();

                            // Setup blacklist or whitelisted apps.
                            foreach(var appName in m_userConfig.BlacklistedApplications)
                            {
                                if(StringExtensions.Valid(appName))
                                {
                                    m_blacklistedApplications.Add(appName);
                                }
                            }

                            foreach(var appName in m_userConfig.WhitelistedApplications)
                            {
                                if(StringExtensions.Valid(appName))
                                {
                                    m_whitelistedApplications.Add(appName);
                                }
                            }

                            // Setup blocking threshold, anti-tamper mechamisms etc.
                            if(m_userConfig.UseThreshold)
                            {
                                // Setup the threshold timers and related data members.
                                InitThresholdData();
                            }

                            if(m_userConfig.CannotTerminate)
                            {
                                // Turn on process protection if requested.
                                CriticalKernelProcessUtility.SetMyProcessAsKernelCritical();
                            }

                            // XXX FIXME Update our dashboard view model if there are bypasses
                            // configured. Force this up to the UI thread because it's a UI model.
                            if(m_userConfig.BypassesPermitted > 0)
                            {
                                m_ipcServer.NotifyRelaxedPolicyChange(m_userConfig.BypassesPermitted, m_userConfig.BypassDuration);
                            }
                            else
                            {
                                m_ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero);
                            }

                            // Recreate our filter collection and reset all categories to be disabled.
                            if(m_filterCollection != null)
                            {
                                
                            }

                            // Recreate our triggers container.
                            if(m_textTriggers != null)
                            {
                                m_textTriggers.Dispose();
                            }

                            // We need to force clearing of all connection pools, then force a
                            // shutdown on the native side of our SQlite managed wrapper in order to
                            // force connections to existing databases to be destroyed. This is
                            // primarily a concern for in-memory databases, because without this
                            // code, those in memory db's will persist so long as any connection is
                            // left open to them.
                            try
                            {
                                SQLiteConnection.ClearAllPools();
                            }
                            catch { }

                            try
                            {
                                SQLiteConnection.Shutdown(true, true);
                            }
                            catch { }

                            m_filterCollection = new FilterDbCollection(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rules.db"), true, true);
                            m_categoryIndex.SetAll(false);

                            // XXX TODO - Maybe make it a compiler flag to toggle if this is going to
                            // be an in-memory DB or not.
                            m_textTriggers = new BagOfTextTriggers(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "t.dat"), true, true);

                            // Now clear all generated categories. These will be re-generated as needed.
                            m_generatedCategoriesMap.Clear();

                            // Now drop all existing NLP models.
                            try
                            {
                                m_doccatSlimLock.EnterWriteLock();
                                m_documentClassifiers.Clear();
                            }
                            finally
                            {
                                m_doccatSlimLock.ExitWriteLock();
                            }

                            // Load all configured NLP models.
                            foreach(var nlpEntry in m_userConfig.ConfiguredNlpModels)
                            {
                                var modelEntry = zip.Entries.Where(pp => pp.FullName.OIEquals(nlpEntry.RelativeModelPath)).FirstOrDefault();
                                if(modelEntry != null)
                                {
                                    using(var mStream = modelEntry.Open())
                                    using(var ms = new MemoryStream())
                                    {
                                        mStream.CopyTo(ms);
                                        LoadNlpModel(ms.ToArray(), nlpEntry);
                                    }
                                }
                            }

                            uint totalFilterRulesLoaded = 0;
                            uint totalFilterRulesFailed = 0;
                            uint totalTriggersLoaded = 0;

                            // Load all configured list files.
                            foreach(var listModel in m_userConfig.ConfiguredLists)
                            {
                                var listEntry = zip.Entries.Where(pp => pp.FullName.OIEquals(listModel.RelativeListPath)).FirstOrDefault();
                                if(listEntry != null)
                                {
                                    var thisListCategoryName = listModel.RelativeListPath.Substring(0, listModel.RelativeListPath.LastIndexOfAny(new[] { '/', '\\' }) + 1) + Path.GetFileNameWithoutExtension(listModel.RelativeListPath);

                                    MappedFilterListCategoryModel categoryModel = null;

                                    switch(listModel.ListType)
                                    {
                                        case PlainTextFilteringListType.Blacklist:
                                        {
                                            if(TryFetchOrCreateCategoryMap(thisListCategoryName, out categoryModel))
                                            {
                                                using(var listStream = listEntry.Open())
                                                {
                                                    var loadedFailedRes = m_filterCollection.ParseStoreRulesFromStream(listStream, categoryModel.CategoryId).Result;
                                                    totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                                                    totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                                                    if(loadedFailedRes.Item1 > 0)
                                                    {
                                                        m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                                                    }
                                                }
                                            }
                                        }
                                        break;

                                        case PlainTextFilteringListType.BypassList:
                                        {
                                            MappedBypassListCategoryModel bypassCategoryModel = null;

                                            // Must be loaded twice. Once as a blacklist, once as a whitelist.
                                            if(TryFetchOrCreateCategoryMap(thisListCategoryName, out bypassCategoryModel))
                                            {
                                                // Load first as blacklist.
                                                using(var listStream = listEntry.Open())
                                                {
                                                    var loadedFailedRes = m_filterCollection.ParseStoreRulesFromStream(listStream, bypassCategoryModel.CategoryId).Result;
                                                    totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                                                    totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                                                    if(loadedFailedRes.Item1 > 0)
                                                    {
                                                        m_categoryIndex.SetIsCategoryEnabled(bypassCategoryModel.CategoryId, true);
                                                    }
                                                }

                                                GC.Collect();

                                                // Load second as whitelist, but start off with the
                                                // category disabled.
                                                using(TextReader tr = new StreamReader(listEntry.Open()))
                                                {
                                                    var bypassAsWhitelistRules = new List<string>();
                                                    string line = null;
                                                    while((line = tr.ReadLine()) != null)
                                                    {
                                                        bypassAsWhitelistRules.Add("@@" + line.Trim() + "\n");
                                                    }

                                                    var loadedFailedRes = m_filterCollection.ParseStoreRules(bypassAsWhitelistRules.ToArray(), bypassCategoryModel.CategoryIdAsWhitelist).Result;
                                                    totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                                                    totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                                                    m_categoryIndex.SetIsCategoryEnabled(bypassCategoryModel.CategoryIdAsWhitelist, false);
                                                }

                                                GC.Collect();
                                            }
                                        }
                                        break;

                                        case PlainTextFilteringListType.TextTrigger:
                                        {
                                            // Always load triggers as blacklists.
                                            if(TryFetchOrCreateCategoryMap(thisListCategoryName, out categoryModel))
                                            {
                                                using(var listStream = listEntry.Open())
                                                {
                                                    var triggersLoaded = m_textTriggers.LoadStoreFromStream(listStream, categoryModel.CategoryId).Result;
                                                    m_textTriggers.FinalizeForRead();

                                                    totalTriggersLoaded += (uint)triggersLoaded;

                                                    if(triggersLoaded > 0)
                                                    {
                                                        m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                                                    }
                                                }
                                            }

                                            GC.Collect();
                                        }
                                        break;

                                        case PlainTextFilteringListType.Whitelist:
                                        {
                                            using(TextReader tr = new StreamReader(listEntry.Open()))
                                            {
                                                if(TryFetchOrCreateCategoryMap(thisListCategoryName, out categoryModel))
                                                {
                                                    var whitelistRules = new List<string>();
                                                    string line = null;
                                                    while((line = tr.ReadLine()) != null)
                                                    {
                                                        whitelistRules.Add("@@" + line.Trim() + "\n");
                                                    }

                                                    using(var listStream = listEntry.Open())
                                                    {
                                                        var loadedFailedRes = m_filterCollection.ParseStoreRules(whitelistRules.ToArray(), categoryModel.CategoryId).Result;
                                                        totalFilterRulesLoaded += (uint)loadedFailedRes.Item1;
                                                        totalFilterRulesFailed += (uint)loadedFailedRes.Item2;

                                                        if(loadedFailedRes.Item1 > 0)
                                                        {
                                                            m_categoryIndex.SetIsCategoryEnabled(categoryModel.CategoryId, true);
                                                        }
                                                    }
                                                }
                                            }

                                            GC.Collect();
                                        }
                                        break;
                                    }
                                }
                            }

                            m_logger.Info("Loaded {0} rules, {1} rules failed most likely due to being malformed, and {2} text triggers loaded.", totalFilterRulesLoaded, totalFilterRulesFailed, totalTriggersLoaded);
                        }
                    }
                }

                if(m_userConfig != null)
                {
                    // Put the new update frequence into effect.
                    this.m_updateCheckTimer.Change(m_userConfig.UpdateFrequency, Timeout.InfiniteTimeSpan);
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
            finally
            {
                m_filteringRwLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Called whenever a relaxed policy has been requested. 
        /// </summary>
        private void OnRelaxedPolicyRequested()
        {
            // Start the count down timer.
            if(m_relaxedPolicyExpiryTimer == null)
            {
                m_relaxedPolicyExpiryTimer = new Timer(OnRelaxedPolicyTimerExpired, null, Timeout.Infinite, Timeout.Infinite);
            }

            // Disable every category that is a bypass category.
            foreach(var entry in m_generatedCategoriesMap.Values)
            {
                if(entry is MappedBypassListCategoryModel)
                {
                    m_categoryIndex.SetIsCategoryEnabled(((MappedBypassListCategoryModel)entry).CategoryIdAsWhitelist, true);
                }
            }

            var cfg = Config;
            m_relaxedPolicyExpiryTimer.Change(cfg != null ? cfg.BypassDuration : TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);

            DecrementRelaxedPolicy();
        }

        private void DecrementRelaxedPolicy()
        {
            bool allUsesExhausted = false;

            var cfg = Config;

            if(cfg != null)
            {
                cfg.BypassesUsed++;

                allUsesExhausted = cfg.BypassesPermitted - cfg.BypassesUsed <= 0;

                if(allUsesExhausted)
                {
                    m_ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero);
                }
                else
                {
                    m_ipcServer.NotifyRelaxedPolicyChange(cfg.BypassesPermitted - cfg.BypassesUsed, cfg.BypassDuration);
                }
            }
            else
            {
                m_ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero);
            }

            if(allUsesExhausted)
            {
                // Refresh tomorrow at midnight.
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var span = tomorrow - DateTime.Now;

                if(m_relaxedPolicyResetTimer == null)
                {
                    m_relaxedPolicyResetTimer = new Timer(OnRelaxedPolicyResetExpired, null, span, Timeout.InfiniteTimeSpan);
                }

                m_relaxedPolicyResetTimer.Change(span, Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Called when the user has manually requested to relinquish a relaxed policy. 
        /// </summary>
        private void OnRelinquishRelaxedPolicyRequested()
        {
            bool relaxedInEffect = false;
            // Determine if a relaxed policy is currently in effect.
            foreach(var entry in m_generatedCategoriesMap.Values)
            {
                if(entry is MappedBypassListCategoryModel)
                {
                    if(m_categoryIndex.GetIsCategoryEnabled(((MappedBypassListCategoryModel)entry).CategoryIdAsWhitelist) == true)
                    {
                        relaxedInEffect = true;
                    }
                }
            }

            // Ensure timer is stopped and re-enable categories by simply calling the timer's expiry callback.
            if(relaxedInEffect)
            {
                OnRelaxedPolicyTimerExpired(null);
            }

            // If a policy was not already in effect, then the user is choosing to relinquish a
            // policy not yet used. So just eat it up. If this is not the case, then the policy has
            // already been decremented, so don't bother.
            if(!relaxedInEffect)
            {
                DecrementRelaxedPolicy();
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
            // Enable every category that is a bypass category.
            foreach(var entry in m_generatedCategoriesMap.Values)
            {
                if(entry is MappedBypassListCategoryModel)
                {
                    m_categoryIndex.SetIsCategoryEnabled(((MappedBypassListCategoryModel)entry).CategoryIdAsWhitelist, false);
                }
            }

            // Disable the expiry timer.
            m_relaxedPolicyExpiryTimer.Change(Timeout.Infinite, Timeout.Infinite);
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
            var cfg = Config;

            if(cfg != null)
            {
                cfg.BypassesUsed = 0;
                m_ipcServer.NotifyRelaxedPolicyChange(cfg.BypassesPermitted, cfg.BypassDuration);
            }

            // Disable the reset timer.
            m_relaxedPolicyResetTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Attempts to read DNS configuration data from the application configuration and then set
        /// those DNS settings on all available non-tunnel adapters.
        /// </summary>
        /// <param name="state">
        /// State object for timer. Always null, unused. 
        /// </param>
        private void TryEnfornceDns(object state = null)
        {
            lock(m_dnsEnforcementLock)
            {
                try
                {

                    if(NetworkStatus.Default.BehindIPv4CaptivePortal || NetworkStatus.Default.BehindIPv6CaptivePortal)
                    {
                        var setDnsForNicToDhcp = new Action<string>((nicName) =>
                        {
                            using(var networkConfigMng = new ManagementClass("Win32_NetworkAdapterConfiguration"))
                            {
                                using(var networkConfigs = networkConfigMng.GetInstances())
                                {
                                    foreach(var managementObject in networkConfigs.Cast<ManagementObject>().Where(objMO => (bool)objMO["IPEnabled"] && objMO["Description"].Equals(nicName)))
                                    {
                                        managementObject.InvokeMethod("SetDNSServerSearchOrder", null, null);
                                    }
                                }
                            }
                        });

                        var ifaces = NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

                        foreach(var iface in ifaces)
                        {
                            setDnsForNicToDhcp(iface.Description);
                        }
                    }
                    else
                    {
                        IPAddress primaryDns = null;
                        IPAddress secondaryDns = null;

                        var cfg = Config;

                        // Check if any DNS servers are defined, and if so, set them.
                        if(cfg != null && StringExtensions.Valid(cfg.PrimaryDns))
                        {
                            IPAddress.TryParse(cfg.PrimaryDns.Trim(), out primaryDns);
                        }

                        if(cfg != null && StringExtensions.Valid(cfg.SecondaryDns))
                        {
                            IPAddress.TryParse(cfg.SecondaryDns.Trim(), out secondaryDns);
                        }

                        if(primaryDns != null || secondaryDns != null)
                        {
                            var setDnsForNic = new Action<string, IPAddress, IPAddress>((nicName, pDns, sDns) =>
                            {
                                using(var networkConfigMng = new ManagementClass("Win32_NetworkAdapterConfiguration"))
                                {
                                    using(var networkConfigs = networkConfigMng.GetInstances())
                                    {
                                        foreach(var managementObject in networkConfigs.Cast<ManagementObject>().Where(objMO => (bool)objMO["IPEnabled"] && objMO["Description"].Equals(nicName)))
                                        {
                                            using(var newDNS = managementObject.GetMethodParameters("SetDNSServerSearchOrder"))
                                            {
                                                List<string> dnsServers = new List<string>();
                                                var existingDns = (string[])newDNS["DNSServerSearchOrder"];
                                                if(existingDns != null && existingDns.Length > 0)
                                                {
                                                    dnsServers = new List<string>(existingDns);
                                                }

                                                bool changed = false;

                                                if(pDns != null)
                                                {
                                                    if(!dnsServers.Contains(pDns.ToString()))
                                                    {
                                                        dnsServers.Insert(0, pDns.ToString());
                                                        changed = true;
                                                    }
                                                }
                                                if(sDns != null)
                                                {
                                                    if(!dnsServers.Contains(sDns.ToString()))
                                                    {
                                                        changed = true;

                                                        if(dnsServers.Count > 0)
                                                        {
                                                            dnsServers.Insert(1, sDns.ToString());
                                                        }
                                                        else
                                                        {
                                                            dnsServers.Add(sDns.ToString());
                                                        }
                                                    }
                                                }

                                                if(changed)
                                                {
                                                    m_logger.Info("Setting DNS for adapter {1} to {0}.", nicName, string.Join(",", dnsServers.ToArray()));
                                                    newDNS["DNSServerSearchOrder"] = dnsServers.ToArray();
                                                    managementObject.InvokeMethod("SetDNSServerSearchOrder", newDNS, null);
                                                }
                                                else
                                                {
                                                    m_logger.Info("No change in DNS settings.");
                                                }
                                            }
                                        }
                                    }
                                }
                            });

                            var ifaces = NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

                            foreach(var iface in ifaces)
                            {
                                bool needsUpdate = false;

                                if(primaryDns != null && !iface.GetIPProperties().DnsAddresses.Contains(primaryDns))
                                {
                                    needsUpdate = true;
                                }
                                if(secondaryDns != null && !iface.GetIPProperties().DnsAddresses.Contains(secondaryDns))
                                {
                                    needsUpdate = true;
                                }

                                if(needsUpdate)
                                {
                                    setDnsForNic(iface.Description, primaryDns, secondaryDns);
                                }
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    LoggerUtil.RecursivelyLogException(m_logger, e);
                }

                if(m_dnsEnforcementTimer == null)
                {
                    m_dnsEnforcementTimer = new Timer(TryEnfornceDns, null, Timeout.Infinite, Timeout.Infinite);
                }
                else
                {
                    m_dnsEnforcementTimer.Change(TimeSpan.FromMinutes(1), Timeout.InfiniteTimeSpan);
                }
            }
        }

        /// <summary>
        /// Stops the filtering engine, shuts it down. 
        /// </summary>
        private void StopFiltering()
        {
            if(m_filteringEngine != null && m_filteringEngine.IsRunning)
            {
                m_filteringEngine.Stop();
            }
        }

        /// <summary>
        /// Attempts to fetch a FilterListEntry instance for the supplied category name, or create a
        /// new one if one does not exist. Whether one is created, or an existing instance is
        /// discovered, a valid, unique FilterListEntry for the supplied category shall be returned.
        /// </summary>
        /// <param name="categoryName">
        /// The category name for which to fetch or generate a new FilterListEntry instance. 
        /// </param>
        /// <returns>
        /// The unique FilterListEntry for the supplied category name, whether an existing instance
        /// was found or a new one was created.
        /// </returns>
        /// <remarks>
        /// This will always fail if more than 255 categories are created! 
        /// </remarks>
        private bool TryFetchOrCreateCategoryMap<T>(string categoryName, out T model) where T : MappedFilterListCategoryModel
        {
            m_logger.Info("CATEGORY {0}", categoryName);

            MappedFilterListCategoryModel existingCategory = null;
            if(!m_generatedCategoriesMap.TryGetValue(categoryName, out existingCategory))
            {
                // We can't generate anymore categories. Sorry, but the rest get ignored.
                if(m_generatedCategoriesMap.Count >= short.MaxValue)
                {
                    m_logger.Error("The maximum number of filtering categories has been exceeded.");
                    model = null;
                    return false;
                }

                if(typeof(T) == typeof(MappedBypassListCategoryModel))
                {
                    MappedFilterListCategoryModel secondCategory = null;

                    if(TryFetchOrCreateCategoryMap(categoryName + "_as_whitelist", out secondCategory))
                    {
                        var newModel = (T)(MappedFilterListCategoryModel)new MappedBypassListCategoryModel((byte)((m_generatedCategoriesMap.Count) + 1), secondCategory.CategoryId, categoryName, secondCategory.CategoryName);
                        m_generatedCategoriesMap.GetOrAdd(categoryName, newModel);
                        model = newModel;
                        return true;
                    }
                    else
                    {
                        model = null;
                        return false;
                    }
                }
                else
                {
                    var newModel = (T)new MappedFilterListCategoryModel((byte)((m_generatedCategoriesMap.Count) + 1), categoryName);
                    m_generatedCategoriesMap.GetOrAdd(categoryName, newModel);
                    model = newModel;
                    return true;
                }
            }

            model = existingCategory as T;
            return true;
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
            KillAllGuis();

            lock(m_cleanShutdownLock)
            {
                if(!m_cleanShutdownComplete)
                {
                    m_ipcServer.Dispose();

                    try
                    {
                        // Pull our critical status.
                        CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }

                    try
                    {
                        // Shut down engine.
                        StopFiltering();                        
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }

                    if(installSafeguards)
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
                        catch(Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }

                        try
                        {
                            var cfg = Config;
                            if(cfg != null && cfg.BlockInternet)
                            {
                                // While we're here, let's disable the internet so that the user
                                // can't browse the web without us. Only do this of course if configured.
                                try
                                {
                                    WFPUtility.DisableInternet();
                                }
                                catch { }
                            }
                        }
                        catch(Exception e)
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

        /// <summary>
        /// Attempts to determine which neighbour application is the GUI and then, if it is not
        /// running already as a user process, start the GUI. This should be used in situations like
        /// when we need to ask the user to authenticate.
        /// </summary>
        private void ReviveGuiForCurrentUser(bool runInTray = false)
        {
            var allFilesWhereIam = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.exe", SearchOption.TopDirectoryOnly);
            
            try
            {
                string guiExePath;
                if(TryGetGuiFullPath(out guiExePath))
                {
                    m_logger.Info("Starting external GUI executable : {0}", guiExePath);

                    if(runInTray)
                    {
                        var sanitizedArgs = "\"" + Regex.Replace("/StartMinimized", @"(\\+)$", @"$1$1") + "\"";
                        var sanitizedPath = "\"" + Regex.Replace(guiExePath, @"(\\+)$", @"$1$1") + "\"" + " " + sanitizedArgs;
                        ProcessExtensions.StartProcessAsCurrentUser(null, sanitizedPath);
                    }
                    else
                    {
                        ProcessExtensions.StartProcessAsCurrentUser(guiExePath);
                    }

                    
                    return;
                }               
            }
            catch(Exception e)
            {
                m_logger.Error("Error enumerating all files.");
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        private void KillAllGuis()
        {
            try
            {
                string guiExePath;
                if(TryGetGuiFullPath(out guiExePath))
                {
                    foreach(var proc in Process.GetProcesses())
                    {
                        try
                        {
                            if(proc.MainModule.FileName.OIEquals(guiExePath))
                            {
                                proc.Kill();
                            }
                        }
                        catch { }
                    }
                }
            }
            catch(Exception e)
            {
                m_logger.Error("Error enumerating processes when trying to kill all GUI instances.");
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        private bool TryGetGuiFullPath(out string fullGuiExePath)
        {
            var allFilesWhereIam = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.exe", SearchOption.TopDirectoryOnly);

            try
            {
                // Get all exe files in the same dir as this service executable.
                foreach(var exe in allFilesWhereIam)
                {
                    try
                    {
                        m_logger.Info("Checking exe : {0}", exe);
                        // Try to get the exe file metadata.
                        var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);

                        // If our description notes that it's a GUI...
                        if(fvi != null && fvi.FileDescription != null && fvi.FileDescription.IndexOf("GUI", StringComparison.OrdinalIgnoreCase) != -1)
                        {
                            fullGuiExePath = exe;
                            return true;
                        }
                    }
                    catch(Exception le)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, le);
                    }
                }
            }
            catch(Exception e)
            {
                m_logger.Error("Error enumerating sibling files.");
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }

            fullGuiExePath = string.Empty;
            return false;
        }
    }
}