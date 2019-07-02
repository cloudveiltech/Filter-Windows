/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Filter.Platform.Common.Extensions;
using Citadel.Core.Windows.Util;
using Citadel.Core.Windows.Util.Update;
using Citadel.IPC;
using Citadel.IPC.Messages;
using CitadelCore.Net.Proxy;
using Filter.Platform.Common.Data.Models;
using DistillNET;
using Microsoft.Win32;
using murrayju.ProcessExtensions;
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
using WindowsFirewallHelper;
using CitadelService.Util;
using FilterProvider.Common.Configuration;

using FirewallAction = CitadelCore.Net.Proxy.FirewallAction;
using Filter.Platform.Common.Util;
using FilterProvider.Common.Services;
using Filter.Platform.Common;
using CitadelService.Platform;
using FilterProvider.Common.Platform;
using Filter.Platform.Common.Net;
using FilterProvider.Common.Data;
using Citadel.Core.WinAPI;
using System.Runtime.InteropServices;
using FilterNativeWindows;
using CitadelCore.Windows.Diversion;
using FilterProvider.Common.Util;
using Filter.Platform.Common.Types;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;

/**
 * TODO:
 *
 *
 * 
 * 
 * 
 * 
 * 
 * 
 * 
 * A lot of this code is obsolete and needs to be trimmed out!
 * 
 * 
 */
namespace CitadelService.Services
{
    public class FilterServiceProvider
    {
        #region Windows Service API

        private CommonFilterServiceProvider m_provider;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetDllDirectory(string dllDirectory);

        public bool Start()
        {
            return Start(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isTestRun"></param>
        /// <returns>This tells the service provider not to start the protective services since this is a test run.</returns>
        public bool Start(bool isTestRun)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var baseDirectory = Path.GetDirectoryName(assembly.Location);

                var dllDirectory = Path.Combine(baseDirectory, Environment.Is64BitProcess ? "x64" : "x86");
                SetDllDirectory(dllDirectory);

                m_logger = LoggerUtil.GetAppWideLogger();

                return m_provider.Start(isTestRun);
            }
            catch (Exception e)
            {
                // Critical failure.
                try
                {
                    EventLog.CreateEventSource("FilterServiceProvider", "Application");
                    EventLog.WriteEntry("FilterServiceProvider", $"Exception occurred before logger was bootstrapped: {e.ToString()}");
                }
                catch (Exception e2)
                {
                    File.AppendAllText(@"C:\FilterServiceProvider.FatalCrashLog.log", $"Fatal crash.\r\n{e.ToString()}\r\n{e2.ToString()}");
                }

                //LoggerUtil.RecursivelyLogException(m_logger, e);
                return false;
            }
        }

        public bool Stop()
        {
            // We always return false because we don't let anyone tell us that we're going to stop.
            return m_provider.Stop();
        }

        public bool Shutdown()
        {
            // Called on a shutdown event.
            return m_provider.Shutdown();
        }

        public void OnSessionChanged()
        {
            m_provider.OnSessionChanged();
        }

        #endregion Windows Service API

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

        /// <summary>
        /// Used for synchronization whenever our NLP model gets updated while we're already initialized. 
        /// </summary>
        private ReaderWriterLockSlim m_doccatSlimLock = new ReaderWriterLockSlim();

#if WITH_NLP
        private List<CategoryMappedDocumentCategorizerModel> m_documentClassifiers = new List<CategoryMappedDocumentCategorizerModel>();
#endif

        //private ProxyServer m_filteringEngine;

        private BackgroundWorker m_filterEngineStartupBgWorker;
        
        private byte[] m_blockedHtmlPage;
        private byte[] m_badSslHtmlPage;

        private static readonly DateTime s_Epoch = new DateTime(1970, 1, 1);

        private static readonly string s_EpochHttpDateTime = s_Epoch.ToString("r");

        /// <summary>
        /// Applications we never ever want to filter. Right now, this is just OS binaries. 
        /// </summary>
        private static readonly HashSet<string> s_foreverWhitelistedApplications = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Constant for port 80 TCP HTTP
        /// </summary>
        private readonly ushort m_httpStandardPort;

        /// <summary>
        /// Constant for port 443 TCP HTTPS
        /// </summary>
        private readonly ushort m_httpsStandardPort;

        /// <summary>
        /// Constant for port 8080 TCP HTTP
        /// </summary>
        private readonly ushort m_httpAltPort;

        /// <summary>
        /// Constant for port 8443 TCP HTTPS
        /// </summary>
        private readonly ushort m_httpsAltPort;

#endregion FilteringEngineVars

        private ReaderWriterLockSlim m_filteringRwLock = new ReaderWriterLockSlim();

        private ReaderWriterLockSlim m_updateRwLock = new ReaderWriterLockSlim();

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
        /// This timer is used to track a 24 hour cooldown period after the exhaustion of all
        /// available relaxed policy uses. Once the timer is expired, it will reset the count to the
        /// config default and then disable itself.
        /// </summary>
        private Timer m_relaxedPolicyResetTimer;

        private AppcastUpdater m_updater = null;

        private ApplicationUpdate m_lastFetchedUpdate = null;

        private ReaderWriterLockSlim m_appcastUpdaterLock = new ReaderWriterLockSlim();

        private TrustManager m_trustManager = new TrustManager();

        /// <summary>
        /// Default ctor. 
        /// </summary>
        public FilterServiceProvider()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (m_logger != null)
                {
                    m_logger.Error((Exception)e.ExceptionObject);
                }
                else
                {
                    File.WriteAllText("filterserviceprovider-unhandled-exception.log", $"Exception occurred: {((Exception)e.ExceptionObject).Message}");
                }
            };

            PlatformTypes.Register<IPlatformDns>((arr) => new WindowsDns());
            PlatformTypes.Register<IWifiManager>((arr) => new WindowsWifiManager());
            PlatformTypes.Register<IPlatformTrust>((arr) => new TrustManager());
            PlatformTypes.Register<ISystemServices>((arr) => new WindowsSystemServices(this));
            PlatformTypes.Register<IVersionProvider>((arr) => new VersionProvider());

            Citadel.Core.Windows.Platform.Init();

            m_provider = new CommonFilterServiceProvider(OnExtension);

            if (BitConverter.IsLittleEndian)
            {
                m_httpAltPort = (ushort)IPAddress.HostToNetworkOrder((short)8080);
                m_httpsAltPort = (ushort)IPAddress.HostToNetworkOrder((short)8443);
                m_httpsStandardPort = (ushort)IPAddress.HostToNetworkOrder((short)443);
                m_httpStandardPort = (ushort)IPAddress.HostToNetworkOrder((short)80);
            }
            else
            {
                m_httpAltPort = ((ushort)8080);
                m_httpsAltPort = ((ushort)8443);
                m_httpsStandardPort = ((ushort)443);
                m_httpStandardPort = ((ushort)80);
            }
        }

        const string SystemAccountIdentifier = "S-1-5-18";
        const string EveryoneIdentifier = "S-1-1-0";

        private void SetDirectoryAsSystemOnly(string name)
        {
            SecurityIdentifier si = new SecurityIdentifier(SystemAccountIdentifier);
            IdentityReference userId = si.Translate(typeof(NTAccount));

            DirectorySecurity security = Directory.GetAccessControl(name);
            FileSystemAccessRule rule = new FileSystemAccessRule(userId, FileSystemRights.FullControl, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow);

            security.SetAccessRule(rule);
            security.SetAccessRuleProtection(true, false);

            Directory.SetAccessControl(name, security);
        }

        private void SetFileAsSystemOnly(string name)
        {
            SecurityIdentifier si = new SecurityIdentifier(SystemAccountIdentifier);
            IdentityReference userId = si.Translate(typeof(NTAccount));

            FileSecurity security = File.GetAccessControl(name);
            FileSystemAccessRule rule = new FileSystemAccessRule(userId, FileSystemRights.FullControl, InheritanceFlags.ObjectInherit, PropagationFlags.InheritOnly, AccessControlType.Allow);

            security.SetAccessRule(rule);
            security.SetAccessRuleProtection(true, false);
            File.SetAccessControl(name, security);
        }

        private void SetServicePermissions(string name)
        {
            SecurityIdentifier si = new SecurityIdentifier(SystemAccountIdentifier);
            SecurityIdentifier everyone = new SecurityIdentifier(EveryoneIdentifier);

            RawSecurityDescriptor securityDescriptor = null;
            if(!Security.GetServiceSecurity(name, out securityDescriptor))
            {
                m_logger.Warn($"Unable to get proper service permissions for {name} because of Win32 error {Marshal.GetLastWin32Error()}");
            }
            else
            {
                while(securityDescriptor.DiscretionaryAcl.Count > 0)
                {
                    securityDescriptor.DiscretionaryAcl.RemoveAce(0);
                }

                ServiceAccessFlags systemFlags = ServiceAccessFlags.WriteOwner | ServiceAccessFlags.WriteDac | ServiceAccessFlags.ReadControl |
                    ServiceAccessFlags.Delete | ServiceAccessFlags.UserDefinedControl | ServiceAccessFlags.Interrogate | ServiceAccessFlags.PauseContinue |
                    ServiceAccessFlags.Stop | ServiceAccessFlags.Start | ServiceAccessFlags.EnumerateDependents | ServiceAccessFlags.QueryStatus |
                    ServiceAccessFlags.QueryConfig;

                ServiceAccessFlags everyoneFlags = ServiceAccessFlags.QueryConfig | ServiceAccessFlags.QueryStatus | ServiceAccessFlags.EnumerateDependents |
                    ServiceAccessFlags.Start | ServiceAccessFlags.Stop | ServiceAccessFlags.PauseContinue | ServiceAccessFlags.Interrogate | ServiceAccessFlags.UserDefinedControl;

                securityDescriptor.DiscretionaryAcl.InsertAce(0, new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, (int)systemFlags, si, false, null));
                securityDescriptor.DiscretionaryAcl.InsertAce(1, new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, (int)everyoneFlags, everyone, false, null));

                if(!Security.SetServiceSecurity(name, securityDescriptor))
                {
                    m_logger.Warn($"Unable to set proper service permissions for {name} because of Win32 error {Marshal.GetLastWin32Error()}");
                }
            }
        }

        private void OnExtension(CommonFilterServiceProvider provider)
        {
            IPCServer server = provider.IPCServer;

            IPathProvider paths = PlatformTypes.New<IPathProvider>();

            Task.Run(async () =>
            {
                ConnectivityCheck.Accessible accessible = ConnectivityCheck.Accessible.Yes;

                try
                {
                    List<ConflictReason> conflicts = ConflictDetection.SearchConflictReason();
                    server.Send<List<ConflictReason>>(IpcCall.ConflictsDetected, conflicts);

                    IFilterAgent agent = PlatformTypes.New<IFilterAgent>();

                    accessible = agent.CheckConnectivity();
                }
                catch(Exception ex)
                {
                    m_logger.Error(ex, "Failed to check connectivity.");
                }

                try
                {
                    WindowsDiverter diverter = new WindowsDiverter(14300, 14301, 14300, 14301);
                    diverter.ConfirmDenyFirewallAccess = this.OnAppFirewallCheck;

                    diverter.Start(0, () =>
                    {
                        m_logger.Info("Diverter was started successfully.");

                        IFilterAgent agent = PlatformTypes.New<IFilterAgent>();
                        ConnectivityCheck.Accessible afterDiverter = agent.CheckConnectivity();

                        if (accessible == ConnectivityCheck.Accessible.Yes && afterDiverter != ConnectivityCheck.Accessible.Yes)
                        {
                            server.Send<bool>(IpcCall.InternetAccessible, false);
                        }
                        else
                        {
                            server.Send<bool>(IpcCall.InternetAccessible, true);
                        }
                    });
                }
                catch(Exception ex)
                {
                    m_logger.Error($"Error occurred while starting the diverter.");
                    LoggerUtil.RecursivelyLogException(m_logger, ex);
                }
                
            });

        }

        private void OnAppSessionEnding(object sender, SessionEndingEventArgs e)
        {
            m_logger.Info("Session ending.");

            // THIS MUST BE DONE HERE ALWAYS, otherwise, we get BSOD.
            CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();

            Environment.Exit((int)ExitCodes.ShutdownWithSafeguards);
        }

        private bool IsStandardHttpPort(ushort port)
        {
            return port == m_httpStandardPort ||
                port == m_httpsStandardPort ||
                port == m_httpAltPort ||
                port == m_httpsAltPort;
        }

    #region EngineCallbacks
    private AppListCheck appListCheck;

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
        public FirewallResponse OnAppFirewallCheck(FirewallRequest request)
        {
            if(!IsStandardHttpPort(request.RemotePort))
            {
                return new FirewallResponse(FirewallAction.DontFilterApplication, null);
            }

            if(appListCheck == null && m_provider.PolicyConfiguration != null)
            {
                appListCheck = new AppListCheck(m_provider.PolicyConfiguration);
            }

            // XXX TODO - The engine shouldn't even tell us about SYSTEM processes and just silently
            // let them through.
            if (request.BinaryAbsolutePath.OIEquals("SYSTEM"))
            {
                return new FirewallResponse(FirewallAction.DontFilterApplication);
            }

            // Lets completely avoid piping anything from the operating system in the filter, with
            // the sole exception of Microsoft edge.
            if((request.BinaryAbsolutePath.IndexOf("MicrosoftEdge", StringComparison.OrdinalIgnoreCase) == -1) && request.BinaryAbsolutePath.IndexOf(@"\Windows\", StringComparison.OrdinalIgnoreCase) != -1)
            {
                lock(s_foreverWhitelistedApplications)
                {
                    if(s_foreverWhitelistedApplications.Contains(request.BinaryAbsolutePath))
                    {
                        return new FirewallResponse(FirewallAction.DontFilterApplication);
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
                if(SFC.SfcIsFileProtected(IntPtr.Zero, request.BinaryAbsolutePath) > 0)
                {
                    lock(s_foreverWhitelistedApplications)
                    {
                        s_foreverWhitelistedApplications.Add(request.BinaryAbsolutePath);
                    }

                    return new FirewallResponse(FirewallAction.DontFilterApplication);
                }
            }

            try
            {
                m_filteringRwLock.EnterReadLock();

                if(m_provider.PolicyConfiguration.BlacklistedApplications.Count == 0 && m_provider.PolicyConfiguration.WhitelistedApplications.Count == 0)
                {
                    // Just filter anything accessing port 80 and 443.
                    m_logger.Debug("1Filtering application: {0}", request.BinaryAbsolutePath);
                    return new FirewallResponse(FirewallAction.FilterApplication);
                }

                var appName = Path.GetFileName(request.BinaryAbsolutePath);

                if(m_provider.PolicyConfiguration.WhitelistedApplications.Count > 0)
                {
                    bool inList = appListCheck.IsAppInWhitelist(request.BinaryAbsolutePath, appName);

                    if(inList)
                    {
                        return new FirewallResponse(FirewallAction.DontFilterApplication);
                    }
                    else
                    {
                        // Whitelist is in effect, and this app is not whitelisted, so force it through.
                        m_logger.Debug("2Filtering application: {0}", request.BinaryAbsolutePath);
                        return new FirewallResponse(FirewallAction.FilterApplication);
                    }
                }

                if(m_provider.PolicyConfiguration.BlacklistedApplications.Count > 0)
                {
                    bool inList = appListCheck.IsAppInBlacklist(request.BinaryAbsolutePath, appName);

                    if(inList)
                    {
                        m_logger.Debug("3Filtering application: {0}", request.BinaryAbsolutePath);
                        return new FirewallResponse(FirewallAction.FilterApplication);
                    }

                    return new FirewallResponse(FirewallAction.DontFilterApplication);
                }

                // This app was not hit by either an enforced whitelist or blacklist. So, by default
                // we will filter everything. We should never get here, but just in case.

                m_logger.Debug("4Filtering application: {0}", request.BinaryAbsolutePath);
                return new FirewallResponse(FirewallAction.FilterApplication);
            }
            catch(Exception e)
            {
                m_logger.Error("Error in {0}", nameof(OnAppFirewallCheck));
                LoggerUtil.RecursivelyLogException(m_logger, e);
                return new FirewallResponse(FirewallAction.DontFilterApplication);
            }
            finally
            {
                m_filteringRwLock.ExitReadLock();
            }
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

        private byte[] getBlockPageWithResolvedTemplates(Uri requestUri, int matchingCategory, List<MappedFilterListCategoryModel> appliedCategories, BlockType blockType = BlockType.None, string triggerCategory = "")
        {
            string blockPageTemplate = UTF8Encoding.Default.GetString(m_blockedHtmlPage);
            
            return Encoding.UTF8.GetBytes(blockPageTemplate);
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

            var cfg = m_provider.PolicyConfiguration.Configuration;

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

            // Disable the timer before we leave.
            this.m_thresholdEnforcementTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public const int LogCleanupIntervalInHours = 12;
        public const int MaxLogAgeInDays = 7;

        private void OnCleanupLogsElapsed(object state)
        {
            this.CleanupLogs();

            if(m_cleanupLogsTimer == null)
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
            string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "logs");

            if(Directory.Exists(directoryPath))
            {
                string[] files = Directory.GetFiles(directoryPath);
                foreach(string filePath in files)
                {
                    FileInfo info = new FileInfo(filePath);

                    DateTime expiryDate = info.LastWriteTime.AddDays(MaxLogAgeInDays);
                    if(expiryDate < DateTime.Now)
                    {
                        info.Delete();
                    }
                }
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
            lock(m_cleanShutdownLock)
            {
                if(!m_cleanShutdownComplete)
                {
                    try
                    {
                        // Pull our critical status.
                        CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();
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
                            var cfg = m_provider.PolicyConfiguration.Configuration;
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
    }
}
