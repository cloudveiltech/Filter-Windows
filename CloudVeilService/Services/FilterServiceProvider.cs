/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Filter.Platform.Common.Extensions;
using CloudVeil.Core.Windows.Util;
using CloudVeil.IPC;
using CloudVeilCore.Net.Proxy;
using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Gui.CloudVeil.Util;

using Filter.Platform.Common.Util;
using FilterProvider.Common.Services;
using Filter.Platform.Common;
using CloudVeilService.Platform;
using FilterProvider.Common.Platform;
using System.Runtime.InteropServices;
using FilterNativeWindows;
using CloudVeilCore.Windows.Diversion;
using System.Security.AccessControl;
using System.Security.Principal;
using FilterProvider.Common.Util;

namespace CloudVeilService.Services
{
    public class FilterServiceProvider
    {
        #region Windows Service API

        private CommonFilterServiceProvider provider;

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

                logger = LoggerUtil.GetAppWideLogger();

                return provider.Start(isTestRun);
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

                //LoggerUtil.RecursivelyLogException(logger, e);
                return false;
            }
        }

        public bool Stop()
        {
            // We always return false because we don't let anyone tell us that we're going to stop.
            return provider.Stop();
        }

        public bool Shutdown()
        {
            // Called on a shutdown event.
            return provider.Shutdown();
        }

        public void OnSessionChanged()
        {
            provider.OnSessionChanged();
        }

        #endregion Windows Service API


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

        private ReaderWriterLockSlim appcastUpdaterLock = new ReaderWriterLockSlim();

        private TrustManager trustManager = new TrustManager();

        /// <summary>
        /// Default ctor. 
        /// </summary>
        public FilterServiceProvider()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (logger != null)
                {
                    logger.Error((Exception)e.ExceptionObject);
                }
                else
                {
                    File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FatalCrashLog.log"), $"Fatal crash. {e.ExceptionObject}");
                }
            };

            PlatformTypes.Register<IPlatformDns>((arr) => new WindowsDns());
            PlatformTypes.Register<IWifiManager>((arr) => new WindowsWifiManager());
            PlatformTypes.Register<IPlatformTrust>((arr) => new TrustManager());
            PlatformTypes.Register<ISystemServices>((arr) => new WindowsSystemServices(this));
            PlatformTypes.Register<IVersionProvider>((arr) => new VersionProvider());

            CloudVeil.Core.Windows.Platform.Init();

            provider = new CommonFilterServiceProvider(OnExtension);
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
                logger.Warn($"Unable to get proper service permissions for {name} because of Win32 error {Marshal.GetLastWin32Error()}");
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
                    logger.Warn($"Unable to set proper service permissions for {name} because of Win32 error {Marshal.GetLastWin32Error()}");
                }
            }
        }

        WindowsDiverter diverter = new WindowsDiverter();
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
                    logger.Error(ex, "Failed to check connectivity.");
                }

                try
                {
                    diverter.UpdatePorts(AppSettings.Default.HttpsPort);
                    this.provider.OnPortsChanged += () =>
                    {
                        diverter.UpdatePorts(AppSettings.Default.HttpsPort);
                    };

                    this.provider.PolicyConfiguration.OnConfigurationLoaded += (sender, e) =>
                    {
                        FillApplicationLists();
                    };

                    diverter.Start(() =>
                    {
                        logger.Info("Diverter was started successfully.");

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
                        FillApplicationLists();
                    });
                }
                catch(Exception ex)
                {
                    logger.Error($"Error occurred while starting the diverter.");
                    LoggerUtil.RecursivelyLogException(logger, ex);
                }
                
            });

        }

        private void FillApplicationLists()
        {
            if(provider == null || provider.PolicyConfiguration == null || provider.PolicyConfiguration.Configuration == null)
            {
                return;
            }

            diverter.CleanApplist();
            foreach(var app in provider.PolicyConfiguration.Configuration.BlacklistedApplications)
            {
                diverter.AddBlackListedApp(app);
            }
            foreach (var app in provider.PolicyConfiguration.Configuration.WhitelistedApplications)
            {
                var processes = Process.GetProcessesByName(app);
                foreach (var process in processes)
                {
                    logger.Info("App: {0}, PID={1}", app, process.Id);
                }
                diverter.AddWhiteListedApp(app);
            }
            diverter.AddWhiteListedApp("windows\\system32"); //everything from that folder
        }

        private void OnAppSessionEnding(object sender, SessionEndingEventArgs e)
        {
            logger.Info("Session ending.");

            // THIS MUST BE DONE HERE ALWAYS, otherwise, we get BSOD.
            CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();

            Environment.Exit((int)ExitCodes.ShutdownWithSafeguards);
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
            lock(cleanShutdownLock)
            {
                if(!cleanShutdownComplete)
                {
                    try
                    {
                        // Pull our critical status.
                        CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
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
                            LoggerUtil.RecursivelyLogException(logger, e);
                        }

                        try
                        {
                            var cfg = provider.PolicyConfiguration.Configuration;
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
