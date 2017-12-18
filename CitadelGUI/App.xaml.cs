/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Extensions;
using Citadel.Core.Windows.Util;
using Citadel.IPC;
using Citadel.IPC.Messages;
using Microsoft.Win32;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Te.Citadel.Extensions;
using Te.Citadel.Services;
using Te.Citadel.UI.ViewModels;
using Te.Citadel.UI.Views;
using Te.Citadel.UI.Windows;
using Te.Citadel.Util;

namespace Te.Citadel
{
    /// <summary>
    /// Interaction logic for App.xaml 
    /// </summary>
    public partial class CitadelApp : Application
    {
        private class ServiceRunner : BaseProtectiveService
        {
            public ServiceRunner() : base("FilterServiceProvider", true)
            {

            }

            public override void Shutdown(ExitCodes code)
            {
                // If our service has exited cleanly while we're running,
                // lets assume that we should exit WITH safeguards.
                // XXX TODO.
                Current.Dispatcher.BeginInvoke((Action)delegate ()
                {
                    Application.Current.Shutdown(code);
                });
            }
        }

        /// <summary>
        /// Used for synchronization when creating run at startup task. 
        /// </summary>
        private ReaderWriterLockSlim m_runAtStartupLock = new ReaderWriterLockSlim();

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
        /// Used to force-query the server whenever we're told to go into
        /// a synchronize-wait state.
        /// </summary>
        private Timer m_synchronizingTimer;

        private object m_synchronizingTimerLockObj = new object();

        /// <summary>
        /// Logger. 
        /// </summary>
        private readonly Logger m_logger;

        /// <summary>
        /// Shown when the program is minimized to the tray. The app is always minimized to the tray
        /// on close.
        /// </summary>
        private System.Windows.Forms.NotifyIcon m_trayIcon;

        /// <summary>
        /// This BackgroundWorker object handles initializing the application off the UI thread.
        /// Allows the splash screen to function.
        /// </summary>
        private BackgroundWorker m_backgroundInitWorker;

        /// <summary>
        /// Primary and only window we use. 
        /// </summary>
        private MainWindow m_mainWindow;

        /// <summary>
        /// Used to ensure synchronized access when setting DNS settings. 
        /// </summary>
        private object m_dnsEnforcementLock = new object();

        #region Views

        /// <summary>
        /// This view is shown whenever a valid auth or re-auth request cannot be performed, or has
        /// never been performed.
        /// </summary>
        private LoginView m_viewLogin;

        /// <summary>
        /// Used to show the user a nice spinny wheel while they wait for something. 
        /// </summary>
        private ProgressWait m_viewProgressWait;

        /// <summary>
        /// Primary view for a subscribed, authenticated user. 
        /// </summary>
        private DashboardView m_viewDashboard;

        /// <summary>
        /// Used to communicate with the filtering back end service. 
        /// </summary>
        private IPCClient m_ipcClient;

        /// <summary>
        /// Tracks whether the captive portal tool tip has been displayed for the given network.
        /// Will be set back to false when captive portal detection goes back to false.
        /// </summary>
        private bool m_captivePortalShownToUser;

        #endregion Views 

        /// <summary>
        /// Default ctor. 
        /// </summary>
        public CitadelApp()
        {
            m_logger = LoggerUtil.GetAppWideLogger();

            string appVerStr = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            appVerStr += " " + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString();
            appVerStr += " " + (Environment.Is64BitProcess ? "x64" : "x86");

            m_logger.Info("CitadelGUI Version: {0}", appVerStr);

            // Enforce good/proper protocols
            ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & ~SecurityProtocolType.Ssl3) | (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);

            this.Startup += CitadelOnStartup;
        }

        private void CitadelOnStartup(object sender, StartupEventArgs e)
        {
            // Here we need to check 2 things. First, we need to check to make sure
            // that our filter service is running. Second, and if the first condition
            // proves to be false, we need to check if we are running as an admin.
            // If we are not admin, we need to schedule a restart of the app to
            // force us to run as admin. If we are admin, then we will create
            // an instance of the service starter class that will take care of
            // forcing our service into existence.
            bool needRestartAsAdmin = false;
            bool mainServiceViable = true;
            try
            {
                var sc = new ServiceController("FilterServiceProvider");

                switch(sc.Status)
                {
                    case ServiceControllerStatus.Stopped:
                    case ServiceControllerStatus.StopPending:
                    {
                        mainServiceViable = false;
                    }
                    break;
                }
            }
            catch(Exception ae)
            {
                mainServiceViable = false;
            }

            if(!mainServiceViable)
            {
                var id = WindowsIdentity.GetCurrent();
                
                var principal = new WindowsPrincipal(id);
                if(principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    needRestartAsAdmin = false;
                }
                else
                {
                    needRestartAsAdmin = id.Owner == id.User;
                }

                if(needRestartAsAdmin)
                {
                    m_logger.Info("Restarting as admin.");

                    try
                    {
                        // Restart program and run as admin
                        ProcessStartInfo updaterStartupInfo = new ProcessStartInfo();
                        updaterStartupInfo.FileName = "cmd.exe";
                        updaterStartupInfo.Arguments = string.Format("/C TIMEOUT {0} && \"{1}\"", 3, Process.GetCurrentProcess().MainModule.FileName);
                        updaterStartupInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        updaterStartupInfo.Verb = "runas";
                        updaterStartupInfo.CreateNoWindow = true;
                        Process.Start(updaterStartupInfo);
                    }
                    catch(Exception se)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, se);
                    }

                    Environment.Exit(-1);
                    return;
                }
                else
                {
                    // Just creating an instance of this will
                    // do the job of forcing our service to start.
                    // Letting it fly off into garbage collection land
                    // should have no effect. The service is self-sustaining
                    // after this point.
                    var provider = new ServiceRunner();
                }
            }

            // Hook the shutdown/logoff event.
            Current.SessionEnding += OnAppSessionEnding;
            
            // Hook app exiting function. This must be done on this main app thread.            
            this.Exit += OnApplicationExiting;

            // Do stuff that must be done on the UI thread first. Here we HAVE to set our initial
            // view state BEFORE we initialize IPC. If we change it after, then we're going to get
            // desynchronized in our state.
            try
            {
                InitViews();
                OnViewChangeRequest(typeof(ProgressWait));
            }
            catch(Exception ve)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ve);
            }

            try
            {
                // XXX FIXME
                m_ipcClient = IPCClient.InitDefault();
                m_ipcClient.AuthenticationResultReceived = (authenticationFailureResult) =>
                {
                    switch(authenticationFailureResult.Action)
                    {
                        case AuthenticationAction.Denied:
                        case AuthenticationAction.Required:
                        case AuthenticationAction.InvalidInput:
                        {
                            // User needs to log in.
                            BringAppToFocus();
                            OnViewChangeRequest(typeof(LoginView));      
                        }
                        break;

                        case AuthenticationAction.Authenticated:
                        case AuthenticationAction.ErrorNoInternet:
                        case AuthenticationAction.ErrorUnknown:                        
                        {
                            OnViewChangeRequest(typeof(DashboardView));
                        }
                        break;
                    }
                };

                m_ipcClient.ServerAppUpdateRequestReceived = async (args) =>
                {
                    string updateSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "update.settings");

                    if(File.Exists(updateSettingsPath))
                    {
                        using (StreamReader reader = File.OpenText(updateSettingsPath))
                        {
                            string command = reader.ReadLine();

                            string[] commandParts = command.Split(new char[] { ':' }, 2);

                            if (commandParts[0] == "RemindLater")
                            {
                                DateTime remindLater;
                                if (DateTime.TryParse(commandParts[1], out remindLater))
                                {
                                    if (DateTime.Now < remindLater)
                                    {
                                        return;
                                    }
                                }
                            }
                            else if (commandParts[0] == "SkipVersion")
                            {
                                if (commandParts[1] == args.NewVersionString)
                                {
                                    return;
                                }
                            }
                        }
                    }

                    BringAppToFocus();

                    var updateAvailableString = string.Format("An update to version {0} is available. You are currently running version {1}. Would you like to update now?", args.NewVersionString, args.CurrentVersionString);
                    
                    if(args.IsRestartRequired)
                    {
                        updateAvailableString += "\r\n\r\nThis update WILL require a reboot. Save all your work before continuing.";
                    }

                    await Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Normal,
                        (Action) async delegate ()
                        {
                            if(m_mainWindow != null)
                            {   
                                var result = await m_mainWindow.AskUserUpdateQuestion("Update Available", updateAvailableString);

                                switch(result)
                                {
                                    case UpdateDialogResult.UpdateNow:
                                        m_ipcClient.NotifyAcceptUpdateRequest();
                                        m_mainWindow.ShowUserMessage("Updating", "The update is being downloaded. The application will automatically update and restart when the download is complete.");
                                        break;

                                    case UpdateDialogResult.RemindLater:
                                        using (StreamWriter writer = new StreamWriter(File.Open(updateSettingsPath, FileMode.Create)))
                                        {
                                            writer.WriteLine("RemindLater:{0}", DateTime.Now.AddDays(1).ToString("o"));
                                        }

                                        break;

                                    case UpdateDialogResult.SkipVersion:
                                        using (StreamWriter writer = new StreamWriter(File.Open(updateSettingsPath, FileMode.Create)))
                                        {
                                            writer.WriteLine("SkipVersion:{0}", args.NewVersionString);
                                        }

                                        break;
                                }
                            }
                        });
                };

                m_ipcClient.ServerUpdateStarting = () =>
                {   
                    Application.Current.Shutdown(ExitCodes.ShutdownForUpdate);
                };

                m_ipcClient.DeactivationResultReceived = (granted) =>
                {
                    if(granted == true)
                    {
                        if(CriticalKernelProcessUtility.IsMyProcessKernelCritical)
                        {
                            CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();
                        }

                        m_logger.Info("Deactivation request granted on client.");

                        // Init the shutdown of this application.
                        Application.Current.Shutdown(ExitCodes.ShutdownWithoutSafeguards);
                    }
                    else
                    {
                        m_logger.Info("Deactivation request denied on client.");

                        Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Normal,
                        (Action)delegate ()
                        {
                            if(m_mainWindow != null)
                            {
                                m_mainWindow.ShowUserMessage("Request Received", "Your deactivation request has been received, but approval is still pending.");
                            }
                        }
                    );
                    }
                };

                m_ipcClient.BlockActionReceived = (args) =>
                {
                    // Add this blocked request to the dashboard.
                    Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Normal,
                        (Action)delegate ()
                        {
                            if(m_viewDashboard != null)
                            {
                                m_viewDashboard.AppendBlockActionEvent(args.Category, args.Resource.ToString());
                            }
                        }
                    );
                };

                m_ipcClient.ConnectedToServer = () =>
                {
                    m_logger.Info("Connected to IPC server.");
                };

                m_ipcClient.DisconnectedFromServer = () =>
                {
                    m_logger.Warn("Disconnected from IPC server! Automatically attempting reconnect.");
                };

                m_ipcClient.RelaxedPolicyExpired = () =>
                {
                    // We don't have to do anything here on our side,
                    // but we may want to do something here in the future
                    // if we modify how our UI shows relaxed policy timer
                    // stuff. Like perhaps changing views etc.
                };

                m_ipcClient.RelaxedPolicyInfoReceived = (args) =>
                {
                    Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Normal,
                        (Action)delegate ()
                        {
                            // Bypass lists have been enabled.
                            if(args.PolicyInfo != null && m_viewDashboard.DataContext != null && m_viewDashboard.DataContext is DashboardViewModel)
                            {
                                var dashboardViewModel = ((DashboardViewModel)m_viewDashboard.DataContext);
                                dashboardViewModel.AvailableRelaxedRequests = args.PolicyInfo.NumberAvailableToday;
                                dashboardViewModel.RelaxedDuration = new DateTime(args.PolicyInfo.RelaxDuration.Ticks).ToString("HH:mm");

                                // Ensure we don't overlap this event multiple times by
                                // decrementing first.
                                dashboardViewModel.Model.RelaxedPolicyRequested -= OnRelaxedPolicyRequested;
                                dashboardViewModel.Model.RelaxedPolicyRequested += OnRelaxedPolicyRequested;

                                // Ensure we don't overlap this event multiple times by
                                // decrementing first.
                                dashboardViewModel.Model.RelinquishRelaxedPolicyRequested -= OnRelinquishRelaxedPolicyRequested;
                                dashboardViewModel.Model.RelinquishRelaxedPolicyRequested += OnRelinquishRelaxedPolicyRequested;
                            }
                        }
                    );
                };

                m_ipcClient.StateChanged = (args) =>
                {
                    m_logger.Info("Filter status from server is: {0}", args.State.ToString());
                    switch(args.State)
                    {
                        case FilterStatus.CooldownPeriodEnforced:
                        {
                            // Add this blocked request to the dashboard.
                            Current.Dispatcher.BeginInvoke(
                                System.Windows.Threading.DispatcherPriority.Normal,
                                (Action)delegate ()
                                {
                                    if(m_viewDashboard != null)
                                    {
                                        m_viewDashboard.ShowDisabledInternetMessage(DateTime.Now.Add(args.CooldownPeriod));
                                    }
                                }
                            );
                        }
                        break;

                        case FilterStatus.Running:
                        {
                            OnViewChangeRequest(typeof(DashboardView));

                            // Change UI state of dashboard to not show disabled message anymore. If
                            // we're not already in a disabled state, this will have no effect.
                            Current.Dispatcher.BeginInvoke(
                                System.Windows.Threading.DispatcherPriority.Normal,
                                (Action)delegate ()
                                {
                                    if(m_viewDashboard != null)
                                    {
                                        m_viewDashboard.HideDisabledInternetMessage();
                                    }
                                }
                            );
                        }
                        break;

                        case FilterStatus.Synchronizing:
                        {   
                            // Update our timestamps for last sync.
                            OnViewChangeRequest(typeof(ProgressWait));

                            lock(m_synchronizingTimerLockObj)
                            {
                                if(m_synchronizingTimer != null)
                                {
                                    m_synchronizingTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                                    m_synchronizingTimer.Dispose();
                                }

                                m_synchronizingTimer = new Timer((state) =>
                                {
                                    m_ipcClient.RequestStatusRefresh();
                                    m_synchronizingTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                                    m_synchronizingTimer.Dispose();
                                });

                                m_synchronizingTimer.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
                            }
                            
                        }
                        break;

                        case FilterStatus.Synchronized:
                        {
                            // Update our timestamps for last sync.
                            OnViewChangeRequest(typeof(DashboardView));

                            // Change UI state of dashboard to not show disabled message anymore. If
                            // we're not already in a disabled state, this will have no effect.
                            Current.Dispatcher.BeginInvoke(
                                System.Windows.Threading.DispatcherPriority.Normal,
                                (Action)delegate ()
                                {
                                    if(m_viewDashboard != null && m_viewDashboard.DataContext != null)
                                    {
                                        var dashboardViewModel = ((DashboardViewModel)m_viewDashboard.DataContext);
                                        dashboardViewModel.LastSync = DateTime.Now;
                                    }
                                }
                            );
                        }
                        break;
                    }
                };

                m_ipcClient.ClientToClientCommandReceived = (args) =>
                {
                    switch(args.Command)
                    {
                        case ClientToClientCommand.ShowYourself:
                        {
                            BringAppToFocus();
                        }
                        break;
                    }
                };

                m_ipcClient.CaptivePortalDetectionReceived = (msg) =>
                {

                    // C# doesn't like cross-thread GUI variable access, so run this on window thread.
                    m_mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        ((MainWindowViewModel)m_mainWindow.DataContext).ShowIsGuestNetwork = msg.IsCaptivePortalDetected;
                    });
                };

#if CAPTIVE_PORTAL_GUI_ENABLED
                m_ipcClient.CaptivePortalDetectionReceived = (msg) =>
                {
                    if (msg.IsCaptivePortalDetected && !m_captivePortalShownToUser)
                    {
                        if (m_mainWindow.Visibility == Visibility.Visible)
                        {
                            if (!m_mainWindow.IsVisible)
                            {
                                BringAppToFocus();
                            }

                            ((MainWindowViewModel)m_mainWindow.DataContext).ShowIsGuestNetwork = true;
                        }
                        else
                        {
                            DisplayCaptivePortalToolTip();
                        }

                        m_captivePortalShownToUser = true;
                    }
                    else if(!msg.IsCaptivePortalDetected)
                    {
                        m_captivePortalShownToUser = false;
                    }
                };
#endif

            }
            catch(Exception ipce)
            {
                LoggerUtil.RecursivelyLogException(m_logger, ipce);
            }

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Run the background init worker for non-UI related initialization.
            m_backgroundInitWorker = new BackgroundWorker();
            m_backgroundInitWorker.DoWork += DoBackgroundInit;
            m_backgroundInitWorker.RunWorkerCompleted += OnBackgroundInitComplete;

            m_backgroundInitWorker.RunWorkerAsync(e);
        }

        private void ScheduleAppRestart(int secondDelay = 30)
        {
            m_logger.Info("Scheduling GUI restart {0} seconds from now.", secondDelay);

            var executingProcess = Process.GetCurrentProcess().MainModule.FileName;

            ProcessStartInfo updaterStartupInfo = new ProcessStartInfo();
            updaterStartupInfo.FileName = "cmd.exe";
            updaterStartupInfo.Arguments = string.Format("/C TIMEOUT {0} && \"{1}\"", secondDelay, executingProcess);
            updaterStartupInfo.WindowStyle = ProcessWindowStyle.Hidden;
            updaterStartupInfo.CreateNoWindow = true;
            Process.Start(updaterStartupInfo);
        }

        private void OnAppSessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            m_logger.Info("Session ending.");

            // THIS MUST BE DONE HERE ALWAYS, otherwise, we get BSOD.
            CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();

            Application.Current.Shutdown(ExitCodes.ShutdownWithSafeguards);

            // Does this cause a hand up??
            // m_ipcClient.Dispose();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception err = e.ExceptionObject as Exception;
            LoggerUtil.RecursivelyLogException(m_logger, err);
        }

        /// <summary>
        /// Called to initialize the various application views on startup. 
        /// </summary>
        private void InitViews()
        {
            m_mainWindow = new Citadel.UI.Windows.MainWindow();

            m_mainWindow.WindowRestoreRequested += (() =>
            {
                BringAppToFocus();
            });

            m_mainWindow.Closing += ((object sender, CancelEventArgs e) =>
            {
                // Don't actually let the window close, just hide it.
                e.Cancel = true;

                if(m_mainWindow.CurrentView.Content == m_viewLogin)
                {
                    Application.Current.Shutdown(ExitCodes.ShutdownWithoutSafeguards);
                    return;
                }

                // When the main window closes, go to tray and show notification.
                MinimizeToTray(true);
            });

            m_viewLogin = new LoginView();

            if(m_viewLogin.DataContext != null && m_viewLogin.DataContext is BaseCitadelViewModel)
            {
                ((BaseCitadelViewModel)(m_viewLogin.DataContext)).ViewChangeRequest = OnViewChangeRequest;
            }

            m_viewProgressWait = new ProgressWait();

            m_viewDashboard = new DashboardView();

            if(m_viewDashboard.DataContext != null && m_viewDashboard.DataContext is BaseCitadelViewModel)
            {
                ((BaseCitadelViewModel)(m_viewDashboard.DataContext)).ViewChangeRequest = OnViewChangeRequest;
                ((BaseCitadelViewModel)(m_viewDashboard.DataContext)).UserNotificationRequest = OnNotifyUserRequest;
            }

            // Set the current view to ProgressWait because we're gonna do background init next.
            this.MainWindow = m_mainWindow;
            m_mainWindow.Show();
            OnViewChangeRequest(typeof(ProgressWait));
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
            // Setup our tray icon.
            InitTrayIcon();

            var startupArgs = e.Argument as StartupEventArgs;

            if(startupArgs != null)
            {
                bool startMinimized = false;
                for(int i = 0; i != startupArgs.Args.Length; ++i)
                {
                    if(startupArgs.Args[i].OIEquals("/StartMinimized"))
                    {
                        startMinimized = true;
                        break;
                    }
                }

                if(startMinimized)
                {
                    MinimizeToTray(false);
                }
            }
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
        private void OnApplicationExiting(object sender, ExitEventArgs e)
        {   
            try
            {
                m_logger.Info("Application shutdown detected with code {0}.", e.ApplicationExitCode);

                // Unhook first.
                this.Exit -= OnApplicationExiting;
            }
            catch(Exception err)
            {
                LoggerUtil.RecursivelyLogException(m_logger, err);
            }

            try
            {
                DoCleanShutdown();

                if(e.ApplicationExitCode == (int)ExitCodes.ShutdownForUpdate)
                {
                    // Give us a nice long minute to restart.
                    // If the user restarts us manually in the meantime who cares
                    // we have a global mutex preventing multiple instance
                    // and this scheduled startup will just not run.
                    ScheduleAppRestart();
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

                Current.Shutdown(-1);
                return;
            }
        }

        /// <summary>
        /// Initializes the m_trayIcon member, loading the icon graphic and hooking appropriate
        /// handlers to respond to user iteraction requesting to bring the application back out of
        /// the tray.
        /// </summary>
        private void InitTrayIcon()
        {
            m_trayIcon = new System.Windows.Forms.NotifyIcon();

            var iconPackUri = new Uri("pack://application:,,,/Resources/appicon.ico");
            var resourceStream = GetResourceStream(iconPackUri);

            m_trayIcon.Icon = new System.Drawing.Icon(resourceStream.Stream);

            m_trayIcon.DoubleClick +=
                delegate (object sender, EventArgs args)
                {
                    BringAppToFocus();
                };

            m_trayIcon.BalloonTipClosed += delegate (object sender, EventArgs args)
            {
                // Windows 10 looks like it likes to hide tray icon when a user clicks on a tool tip.
                // Force it to stay visible.
                m_trayIcon.Visible = true;
            };

            var menuItems = new List<System.Windows.Forms.MenuItem>();
            menuItems.Add(new System.Windows.Forms.MenuItem("Open", TrayIcon_Open));
            menuItems.Add(new System.Windows.Forms.MenuItem("Settings", TrayIcon_OpenSettings));
            menuItems.Add(new System.Windows.Forms.MenuItem("Use Relaxed Policy", TrayIcon_UseRelaxedPolicy));
            
            m_trayIcon.ContextMenu = new System.Windows.Forms.ContextMenu(menuItems.ToArray());
        }

        private void TrayIcon_Open(object sender, EventArgs e)
        {
            BringAppToFocus();
        }

        private void TrayIcon_OpenSettings(object sender, EventArgs e)
        {
            BringAppToFocus();
            m_viewDashboard.SwitchTab(1);
        }

        private void TrayIcon_UseRelaxedPolicy(object sender, EventArgs e)
        {
            OnRelaxedPolicyRequested(true);
        }

        /// <summary>
        /// Brings the main application window into focus for the user and removes it from the tray
        /// if the application icon is in the tray.
        /// </summary>
        public void BringAppToFocus()
        {
            Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    if(this.m_mainWindow != null)
                    {
                        this.m_mainWindow.Show();
                        this.m_mainWindow.WindowState = WindowState.Normal;
                        this.m_mainWindow.Topmost = true;
                        this.m_mainWindow.Topmost = false;
                    }

                    if(m_trayIcon != null)
                    {
                        m_trayIcon.Visible = false;
                    }
                }
            );
        }

#if CAPTIVE_PORTAL_GUI_ENABLED
        public void DisplayCaptivePortalToolTip()
        {
            m_trayIcon.BalloonTipClicked += captivePortalToolTipClicked;
            m_trayIcon.ShowBalloonTip(6000, "Captive Portal Detected", "This network requires logon information. Click here to continue.", System.Windows.Forms.ToolTipIcon.Info);
        }
#endif

        private void captivePortalToolTipClicked(object sender, EventArgs e)
        {
            m_trayIcon.BalloonTipClicked -= captivePortalToolTipClicked;

            System.Diagnostics.Process.Start("http://connectivitycheck.cloudveil.org");
        }

        /// <summary>
        /// Handles a request from UserControls within the application to show a requested primary
        /// view type.
        /// </summary>
        /// <param name="viewType">
        /// The type of view requested. 
        /// </param>
        private void OnViewChangeRequest(Type viewType)
        {
            try
            {
                Current.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    (Action)delegate ()
                    {
                        UIElement current = (UserControl)m_mainWindow.CurrentView.Content;
                        UIElement newView = null;

                        switch(viewType.Name)
                        {
                            case nameof(LoginView):
                            {
                                newView = m_viewLogin;
                            }
                            break;

                            case nameof(ProgressWait):
                            {
                                newView = m_viewProgressWait;
                            }
                            break;

                            case nameof(DashboardView):
                            {
                                newView = m_viewDashboard;
                            }
                            break;
                        }

                        if(newView != null && current != newView)
                        {
                            m_mainWindow.CurrentView.Content = newView;
                        }
                    }
                );
            }
            catch(Exception err)
            {
                LoggerUtil.RecursivelyLogException(m_logger, err);
            }
        }

        /// <summary>
        /// Called when a view or view model is requesting to post information to the user via a modal. 
        /// </summary>
        /// <param name="title">
        /// The title. 
        /// </param>
        /// <param name="message">
        /// The message. 
        /// </param>
        private void OnNotifyUserRequest(string title, string message)
        {
            m_mainWindow.ShowUserMessage(title, message);
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
            // Get the default FireFox profiles path.
            string defaultFirefoxProfilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            defaultFirefoxProfilesPath += @"\Mozilla\Firefox\Profiles";

            if(!Directory.Exists(defaultFirefoxProfilesPath))
            {
                return;
            }

            // Figure out if firefox is running. If later it is and we kill it, store the path to
            // firefox.exe so we can restart the process after we're done.
            string firefoxExePath = string.Empty;
            bool firefoxIsRunning = Process.GetProcessesByName("firefox").Length > 0;

            var prefsFiles = Directory.GetFiles(defaultFirefoxProfilesPath, "prefs.js", SearchOption.AllDirectories);

            var valuesThatNeedToBeSet = new Dictionary<string, string>();

            var firefoxUserCfgValuesUri = new Uri("pack://application:,,,/Resources/FireFoxUserCFG.txt");
            var resourceStream = GetResourceStream(firefoxUserCfgValuesUri);

            using(TextReader tsr = new StreamReader(resourceStream.Stream))
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

            foreach(var prefFile in prefsFiles)
            {
                var userFile = Path.GetDirectoryName(prefFile) + Path.DirectorySeparatorChar + "user.js";

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
                using(var p = new Process())
                {
                    p.StartInfo.FileName = firefoxExePath;
                    p.StartInfo.UseShellExecute = false;
                    p.Start();
                }
            }
        }

        private void OnRelaxedPolicyRequested()
        {
            OnRelaxedPolicyRequested(false);
        }

        /// <summary>
        /// Called whenever a relaxed policy has been requested. 
        /// </summary>
        private async void OnRelaxedPolicyRequested(bool fromTray)
        {
            using(var ipcClient = new IPCClient())
            {
                ipcClient.ConnectedToServer = () =>
                {
                    ipcClient.RequestRelaxedPolicy();
                };

                ipcClient.RelaxedPolicyInfoReceived += delegate (RelaxedPolicyMessage msg)
                {
                    if (fromTray)
                    {
                        m_trayIcon.ShowBalloonTip(3000, "Relaxed Policy", string.Format("Relaxed policy granted. It will expire in {0} minutes.", (int)msg.PolicyInfo.RelaxDuration.TotalMinutes), System.Windows.Forms.ToolTipIcon.Info);
                    }
                };

                ipcClient.WaitForConnection();
                await Task.Delay(3000);
            }
        }

        /// <summary>
        /// Called when the user has manually requested to relinquish a relaxed policy. 
        /// </summary>
        private async void OnRelinquishRelaxedPolicyRequested()
        {
            using(var ipcClient = new IPCClient())
            {
                ipcClient.ConnectedToServer = () =>
                {
                    ipcClient.RelinquishRelaxedPolicy();
                };

                ipcClient.WaitForConnection();
                await Task.Delay(3000);
            }
        }

        /// <summary>
        /// Sends the application to the task tray, optionally showing a tooltip explaining that the
        /// application is now hiding away.
        /// </summary>
        /// <param name="showTip">
        /// Bool that determines if a short tooltip explaining that the application is now in the background. 
        /// </param>
        private void MinimizeToTray(bool showTip = false)
        {
            Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    if(m_mainWindow != null && m_trayIcon != null)
                    {
                        m_trayIcon.Visible = true;
                        m_mainWindow.Visibility = Visibility.Hidden;

                        if(showTip)
                        {
                            m_trayIcon.ShowBalloonTip(1500, "Still Running", string.Format("{0} will continue running in the background.", Process.GetCurrentProcess().ProcessName), System.Windows.Forms.ToolTipIcon.Info);
                        }
                    }
                }
            );
        }

        // XXX FIXME
        private void DoCleanShutdown()
        {
            lock(m_cleanShutdownLock)
            {
                if(!m_cleanShutdownComplete)
                {
                    m_ipcClient.Dispose();

                    try
                    {
                        // Pull our icon from the task tray.
                        if(m_trayIcon != null)
                        {
                            m_trayIcon.Visible = false;
                        }
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }

                    try
                    {
                        // Pull our critical status.
                        CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, e);
                    }

                    // Flag that clean shutdown was completed already.
                    m_cleanShutdownComplete = true;
                }
            }
        }
    }
}