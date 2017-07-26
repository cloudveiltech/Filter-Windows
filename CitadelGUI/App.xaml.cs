/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Extensions;
using Citadel.Core.Windows.Util;
using Citadel.IPC;
using Citadel.IPC.Messages;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Principal;
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

        private ServiceRunner m_serviceRunner;

        #endregion Views

        /// <summary>
        /// Gets whether or not a startup task exists for this application. 
        /// </summary>
        /// <returns>
        /// True if a startup task exists for this application, false otherwise. 
        /// </returns>
        public bool CheckIfStartupTaskExists()
        {
            try
            {
                m_runAtStartupLock.EnterReadLock();

                using(var ts = new Microsoft.Win32.TaskScheduler.TaskService())
                {
                    var t = ts.GetTask(Process.GetCurrentProcess().ProcessName);
                    if(t == null)
                    {
                        return false;
                    }

                    return true;
                }
            }
            finally
            {
                m_runAtStartupLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Forces the installation of a startup task for this application, removing any existing
        /// task scheduler entries for this application before doing so. The task is installed to run
        /// with maximum priority, and to have task scheduler simply fire and forget, rather than
        /// monitoring the task and restarting it, or setting the task run duration and such. The
        /// task is simply set to run indefinitely at maximum priority at user login.
        /// </summary>
        public void EnsureStarupTaskExists()
        {
            try
            {
                m_runAtStartupLock.EnterWriteLock();

                using(var ts = new Microsoft.Win32.TaskScheduler.TaskService())
                {
                    // Start off by deleting existing tasks always.
                    ts.RootFolder.DeleteTask(Process.GetCurrentProcess().ProcessName, false);

                    // Create a new task definition and assign properties
                    using(var td = ts.NewTask())
                    {
                        td.Principal.RunLevel = Microsoft.Win32.TaskScheduler.TaskRunLevel.Highest;
                        td.Principal.UserId = WindowsIdentity.GetCurrent().Name;
                        td.Principal.LogonType = Microsoft.Win32.TaskScheduler.TaskLogonType.InteractiveToken;

                        td.Settings.Priority = ProcessPriorityClass.RealTime;
                        td.Settings.DisallowStartIfOnBatteries = false;
                        td.Settings.StopIfGoingOnBatteries = false;
                        td.Settings.WakeToRun = false;
                        td.Settings.AllowDemandStart = false;
                        td.Settings.IdleSettings.RestartOnIdle = false;
                        td.Settings.IdleSettings.StopOnIdleEnd = false;
                        td.Settings.RestartCount = 0;
                        td.Settings.AllowHardTerminate = false;
                        //td.Settings.RunOnlyIfLoggedOn = true;
                        td.Settings.Hidden = true;
                        td.Settings.Volatile = false;
                        td.Settings.Enabled = true;
                        td.Settings.Compatibility = Microsoft.Win32.TaskScheduler.TaskCompatibility.V2;
                        td.Settings.ExecutionTimeLimit = TimeSpan.Zero;

                        td.RegistrationInfo.Description = "Runs the content filter at startup.";

                        // Create a trigger that will fire the task at this time every other day
                        var logonTrigger = new Microsoft.Win32.TaskScheduler.LogonTrigger();
                        logonTrigger.Enabled = true;
                        logonTrigger.Repetition.StopAtDurationEnd = false;
                        logonTrigger.ExecutionTimeLimit = TimeSpan.Zero;
                        td.Triggers.Add(logonTrigger);

                        // Create an action that will launch Notepad whenever the trigger fires
                        td.Actions.Add(new Microsoft.Win32.TaskScheduler.ExecAction(Process.GetCurrentProcess().MainModule.FileName, "/StartMinimized", null));

                        // Register the task in the root folder
                        ts.RootFolder.RegisterTaskDefinition(Process.GetCurrentProcess().ProcessName, td, Microsoft.Win32.TaskScheduler.TaskCreation.CreateOrUpdate, WindowsIdentity.GetCurrent().Name, null, Microsoft.Win32.TaskScheduler.TaskLogonType.InteractiveToken);
                    }
                }
            }
            finally
            {
                m_runAtStartupLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Default ctor. 
        /// </summary>
        public CitadelApp()
        {
            m_logger = LoggerUtil.GetAppWideLogger();

            m_serviceRunner = new ServiceRunner();

            // Enforce good/proper protocols
            ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & ~SecurityProtocolType.Ssl3) | (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);

            this.Startup += CitadelOnStartup;
        }

        private void CitadelOnStartup(object sender, StartupEventArgs e)
        {
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
                m_ipcClient = new IPCClient(true);
                m_ipcClient.AuthenticationResultReceived = (args) =>
                {
                    m_logger.Info("Auth response from server is: {0}", args.ToString());
                    switch(args)
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

                m_ipcClient.DeactivationResultReceived = (granted) =>
                {
                    if(granted == true)
                    {
                        if(ProcessProtection.IsProtected)
                        {
                            ProcessProtection.Unprotect();
                        }

                        m_logger.Info("Deactivation request granted on client.");

                        // Init the shutdown of this application.
                        m_ipcClient.Dispose();
                        Dispatcher.BeginInvoke((Action)delegate ()
                        {
                            Application.Current.Shutdown(ExitCodes.ShutdownWithoutSafeguards);
                        });
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

                        case FilterStatus.ExitingWithoutSafeguards:
                        {
                            m_logger.Info("Client shutdown without safeguards command received.");
                            m_ipcClient.Dispose();

                            Dispatcher.BeginInvoke((Action)delegate ()
                            {
                                Application.Current.Shutdown(ExitCodes.ShutdownWithoutSafeguards);
                            });
                            
                        }
                        break;

                        case FilterStatus.ExitingWithSafeguards:
                        {
                            m_logger.Info("Client shutdown with safeguards command received.");
                            m_ipcClient.Dispose();

                            Dispatcher.BeginInvoke((Action)delegate ()
                            {
                                Application.Current.Shutdown(ExitCodes.ShutdownWithoutSafeguards);
                            });
                        }
                        break;

                        case FilterStatus.Running:
                        {
                            BringAppToFocus();

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
                            OnViewChangeRequest(typeof(ProgressWait));
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

        private void OnAppSessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            m_logger.Info("Session ending.");

            // THIS MUST BE DONE HERE ALWAYS, otherwise, we get BSOD.
            ProcessProtection.Unprotect();

            m_ipcClient.Dispose();
            Current.Dispatcher.BeginInvoke((Action)delegate ()
            {
                Application.Current.Shutdown(ExitCodes.ShutdownWithSafeguards);
            });
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

            // XXX FIXME Force start our cascade of protective processes.
            /*
            try
            {
                ServiceSpawner.Instance.InitializeServices();
            }
            catch(Exception se)
            {
                LoggerUtil.RecursivelyLogException(m_logger, se);
            }
            */

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
                if(e.ApplicationExitCode == (int)ExitCodes.ShutdownWithoutSafeguards)
                {
                    DoCleanShutdown(false);
                }
                else
                {
                    // Enforce that our error code is set according to our own understanding.
                    // Anything less than 100 will indicate that the application was not terminated correctly.
                    e.ApplicationExitCode = (int)ExitCodes.ShutdownWithoutSafeguards;

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
                    }

                    if(m_trayIcon != null)
                    {
                        m_trayIcon.Visible = false;
                    }
                }
            );
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

        /// <summary>
        /// Called whenever a relaxed policy has been requested. 
        /// </summary>
        private async void OnRelaxedPolicyRequested()
        {
            using(var ipcClient = new IPCClient())
            {
                ipcClient.ConnectedToServer = () =>
                {
                    ipcClient.RequestRelaxedPolicy();
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
        private void DoCleanShutdown(bool ensureRunAtStartup)
        {
            lock(m_cleanShutdownLock)
            {
                if(!m_cleanShutdownComplete)
                {
                    m_ipcClient.Dispose();

                    if(ensureRunAtStartup)
                    {
                        try
                        {   
                            EnsureStarupTaskExists();
                        }
                        catch(Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }

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
                        ProcessProtection.Unprotect();
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