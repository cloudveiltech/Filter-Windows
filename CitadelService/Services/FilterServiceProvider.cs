/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Filter.Platform.Common.Extensions;
using Citadel.Core.Windows.Util;
using Citadel.IPC;
using CitadelCore.Net.Proxy;
using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Te.Citadel.Util;
using CitadelService.Util;

using FirewallAction = CitadelCore.Net.Proxy.FirewallAction;
using Filter.Platform.Common.Util;
using FilterProvider.Common.Services;
using Filter.Platform.Common;
using CitadelService.Platform;
using FilterProvider.Common.Platform;
using Citadel.Core.WinAPI;
using System.Runtime.InteropServices;
using FilterNativeWindows;
using CitadelCore.Windows.Diversion;
using System.Security.AccessControl;
using System.Security.Principal;

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
                    WindowsDiverter diverter = new WindowsDiverter(14301, 14301, 14301, 14301);
                    diverter.ConfirmDenyFirewallAccess = this.OnAppFirewallCheck;

                    diverter.Start(1, () =>
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
                m_provider.PolicyConfiguration.PolicyLock.EnterReadLock();

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
                m_provider?.PolicyConfiguration?.PolicyLock?.ExitReadLock();
            }
        }
#endregion EngineCallbacks

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
