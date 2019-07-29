/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Filter.Platform.Common.Extensions;
using Citadel.Core.Windows.Util;
using Citadel.Core.Windows;
using Citadel.IPC;
using Citadel.IPC.Messages;
using Filter.Platform.Common.Util;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Te.Citadel.Extensions;
using Te.Citadel.Services;
using Te.Citadel.UI;
using Te.Citadel.UI.ViewModels;
using Te.Citadel.UI.Views;
using Te.Citadel.UI.Windows;
using Te.Citadel.Util;
using Filter.Platform.Common;
using CloudVeilGUI.Platform.Common;
using Filter.Platform.Common.Net;
using Filter.Platform.Common.Client;
using Filter.Platform.Common.Data.Models;
using Citadel.Core.Windows.Util.Update;
using Filter.Platform.Common.Types;
using Filter.Platform.Common.IPC.Messages;
using System.Reflection;
using FilterNativeWindows;
using Te.Citadel;

namespace CloudVeil.Windows
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
                // If our service has exited cleanly while we're running, lets assume that we should
                // exit WITH safeguards. XXX TODO.
                Current.Dispatcher.BeginInvoke((Action)delegate ()
                {
                    Application.Current.Shutdown((int)code);
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
        /// Used to force-query the server whenever we're told to go into a synchronize-wait state. 
        /// </summary>
        private Timer m_synchronizingTimer;

        private object m_synchronizingTimerLockObj = new object();

        /// <summary>
        /// Used to track whether we should allow view changes away from ProgressWait when filter state has not yet been fetched.
        /// </summary>
        private bool m_hasStateBeenFetched = false;

        private AppConfigModel m_appConfig = null;

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
        public ModelManager ModelManager { get; private set; }

        /// <summary
        /// Stores all the views that we want to keep alive.
        /// </summary>
        private ViewManager viewManager;

        public ViewManager ViewManager => viewManager;

        /// <summary>
        /// Used to communicate with the filtering back end service. 
        /// </summary>
        private IPCClient m_ipcClient;

        public IPCClient IpcClient => m_ipcClient;
        /// <summary>
        /// Tracks whether the captive portal tool tip has been displayed for the given network. Will
        /// be set back to false when captive portal detection goes back to false.
        /// </summary>
        private bool m_captivePortalShownToUser;

        #endregion Views

        public Dictionary<string, CertificateExemptionMessage> SslExemptions { get; set; }

        /// <summary>
        /// Default ctor. 
        /// </summary>
        public CitadelApp()
        {
            m_logger = LoggerUtil.GetAppWideLogger();

            string appVerStr = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            appVerStr += " " + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString(3);
            appVerStr += " " + (Environment.Is64BitProcess ? "x64" : "x86");

            m_logger.Info("CitadelGUI Version: {0}", appVerStr);

            // Enforce good/proper protocols
            ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & ~SecurityProtocolType.Ssl3) | (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);

            this.Startup += CitadelOnStartup;
        }

        private void RunGuiChecks()
        {
            IGUIChecks guiChecks = PlatformTypes.New<IGUIChecks>();

            // First, lets check to see if the user started the GUI in an isolated session.
            try
            {
                if (guiChecks.IsInIsolatedSession())
                {
                    LoggerUtil.GetAppWideLogger().Error("GUI client start in an isolated session. This should not happen.");
                    Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                    return;
                }
            }
            catch
            {
                Environment.Exit((int)ExitCodes.ShutdownWithoutSafeguards);
                return;
            }

            try
            {
                bool createdNew = false;
                if (guiChecks.PublishRunningApp())
                {
                    createdNew = true;
                }

                /**/

                if (!createdNew)
                {
                    try
                    {
                        guiChecks.DisplayExistingUI();
                    }
                    catch (Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
                    }

                    // In case we have some out of sync state where the app is running at a higher
                    // privilege level than us, the app won't get our messages. So, let's attempt an
                    // IPC named pipe to deliver the message as well.
                    try
                    {
                        // Something about instantiating an IPCClient here is making it all blow up in my face.
                        using (var ipcClient = IPCClient.InitDefault())
                        {
                            ipcClient.RequestPrimaryClientShowUI();

                            // Wait plenty of time before dispose to allow delivery of the message.
                            Task.Delay(500).Wait();
                        }
                    }
                    catch (Exception e)
                    {
                        // The only way we got here is if the server isn't running, in which case we
                        // can do nothing because its beyond our domain.
                        LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
                    }

                    LoggerUtil.GetAppWideLogger().Info("Shutting down process since one is already open.");

                    // Close this instance.
                    Environment.Exit((int)ExitCodes.ShutdownProcessAlreadyOpen);
                    return;
                }
            }
            catch (Exception e)
            {
                // The only way we got here is if the server isn't running, in which case we can do
                // nothing because its beyond our domain.
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
                return;
            }
        }

        private void CitadelOnStartup(object sender, StartupEventArgs e)
        {
            Citadel.Core.Windows.Platform.Init();

            var filterAgent = PlatformTypes.New<IFilterAgent>();
            filterAgent.StartFilter();

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

                                m_mainWindow.Dispatcher.InvokeAsync(() =>
                                {
                                    ((MainWindowViewModel)m_mainWindow.DataContext).IsUserLoggedIn = false;
                                });

                                viewManager.PushView(LoginView.ModalZIndex, typeof(LoginView));
                            }
                            break;

                        case AuthenticationAction.Authenticated:
                        case AuthenticationAction.ErrorNoInternet:
                        case AuthenticationAction.ErrorUnknown:
                            {
                                m_logger.Info($"The logged in user is {authenticationFailureResult.Username}");

                                m_mainWindow.Dispatcher.InvokeAsync(() =>
                                {
                                    ((MainWindowViewModel)m_mainWindow.DataContext).LoggedInUser = authenticationFailureResult.Username;
                                    ((MainWindowViewModel)m_mainWindow.DataContext).IsUserLoggedIn = true;
                                });

                                // This code prevents the progress->dashboard->progress flash for authenticated users, but not for error'd users.
                                if (authenticationFailureResult.Action != AuthenticationAction.Authenticated)
                                {
                                    viewManager.PopView(typeof(LoginView));
                                }
                                else if (m_hasStateBeenFetched)
                                {
                                    viewManager.PopView(typeof(LoginView));
                                }
                            }
                            break;
                    }
                };

                m_ipcClient.RegisterResponseHandler<ConfigCheckInfo>(IpcCall.SynchronizeSettings, (msg) =>
                {
                    var vm = ModelManager.Get<AdvancedViewModel>();
                    vm.OnSettingsSynchronized(msg);
                    return true;
                });

                m_ipcClient.RegisterResponseHandler<UpdateCheckInfo>(IpcCall.CheckForUpdates, (msg) =>
                {
                    var vm = ModelManager.Get<AdvancedViewModel>();
                    vm.OnCheckForUpdates(msg);
                    return true;
                });

                m_ipcClient.RegisterResponseHandler<ApplicationUpdate>(IpcCall.Update, (msg) =>
                {
                    var vm = ModelManager.Get<AdvancedViewModel>();

                    if(msg.Data.CurrentVersion >= msg.Data.UpdateVersion)
                    {
                        return true;
                    }

                    BringAppToFocus();

                    this.BeginUpdateRequest(msg.Data);

                    return true;
                });

                m_ipcClient.RegisterResponseHandler<object>(IpcCall.InstallerDownloadStarted, (msg) =>
                {
                    m_mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        m_mainWindow.ViewModel.DownloadFlyoutIsOpen = true;
                        m_mainWindow.ViewModel.DownloadProgress = 0;
                    });

                    return true;
                });

                m_ipcClient.RegisterResponseHandler<int>(IpcCall.InstallerDownloadProgress, (msg) =>
                {
                    m_mainWindow.Dispatcher.InvokeAsync(() => m_mainWindow.ViewModel.DownloadProgress = msg.Data);

                    return true;
                });

                m_ipcClient.RegisterResponseHandler<bool>(IpcCall.InstallerDownloadFinished, (msg) =>
                {
                    if (msg.Data)
                    {
                        m_mainWindow.Dispatcher.InvokeAsync(() => m_mainWindow.ViewModel.DownloadProgress = 100);

                        Task.Run(async () =>
                        {
                            await Task.Delay(3000);
                            await m_mainWindow.Dispatcher.InvokeAsync(() => m_mainWindow.ViewModel.DownloadFlyoutIsOpen = false);
                        });
                    }
                    else
                    {
                        m_mainWindow.Dispatcher.InvokeAsync(() => m_mainWindow.ShowUserMessage("Update Failed", "Failed to download the update file."));
                    }

                    return true;
                });

                m_ipcClient.RegisterRequestHandler(IpcCall.ShutdownForUpdate, (msg) =>
                {
                    Application.Current.Shutdown((int)ExitCodes.ShutdownForUpdate);
                    return true;
                });

                m_ipcClient.RegisterResponseHandler<List<ConflictReason>>(IpcCall.ConflictsDetected, (msg) =>
                {
                    string message;
                    string header;

                    // Rather than viewManager.PushView, we want to show the conflict reasons
                    if (msg.Data != null)
                    {
                        List<ConflictInfo> conflicts = new List<ConflictInfo>();

                        foreach (var reason in msg.Data)
                        {
                            ConflictInfo info = new ConflictInfo();

                            if (ConflictReasonInformation.ConflictReasonMessages.TryGetValue(reason, out message))
                            {
                                info.Message = message;
                            }

                            if (ConflictReasonInformation.ConflictReasonHeaders.TryGetValue(reason, out header))
                            {
                                info.Header = header;
                            }

                            info.ConflictReason = reason;

                            conflicts.Add(info);
                        }

                        m_mainWindow.Dispatcher.InvokeAsync(() =>
                        {
                            m_mainWindow.ViewModel.ConflictReasons.Clear();
                            
                            foreach(var conflict in conflicts)
                            {
                                m_mainWindow.ViewModel.ConflictReasons.Add(conflict);
                            }
                        });
                    }
                    else
                    {
                        m_mainWindow.Dispatcher.InvokeAsync(() => m_mainWindow.ViewModel.ConflictReasons.Clear());
                    }

                    return true;
                });

                m_ipcClient.RegisterResponseHandler<string>(IpcCall.ActivationIdentifier, (msg) =>
                {
                    {
                        var vm = ModelManager.Get<SupportViewModel>();
                        m_mainWindow.Dispatcher.Invoke(() => vm.ActivationIdentifier = msg.Data);
                    }

                    {
                        var vm = ModelManager.Get<SelfModerationViewModel>();
                        m_mainWindow.Dispatcher.Invoke(() => vm.ActivationIdentifier = msg.Data);
                    }

                    return true;
                });

                m_ipcClient.DeactivationResultReceived = (deactivationCmd) =>
                {
                    m_logger.Info("Deactivation command is: {0}", deactivationCmd.ToString());

                    if(deactivationCmd == DeactivationCommand.Granted)
                    {
                        if(CriticalKernelProcessUtility.IsMyProcessKernelCritical)
                        {
                            CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();
                        }

                        m_logger.Info("Deactivation request granted on client.");

                        // Init the shutdown of this application.
                        Application.Current.Shutdown((int)ExitCodes.ShutdownWithoutSafeguards);
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
                                string message = null;
                                string title = null;

                                switch(deactivationCmd)
                                {
                                    case DeactivationCommand.Requested:
                                    message = "Your deactivation request has been received, but approval is still pending.";
                                    title = "Request Received";
                                    break;

                                    case DeactivationCommand.Denied:
                                    // A little bit of tact will keep the mob and their pitchforks
                                    // from slaughtering us.
                                    message = "Your deactivation request has been received, but approval is still pending.";
                                    title = "Request Received";
                                    //message = "Your deactivation request has been denied.";
                                    //title = "Request Denied";
                                    break;

                                    case DeactivationCommand.Granted:
                                    message = "Your request was granted.";
                                    title = "Request Granted";
                                    break;

                                    case DeactivationCommand.NoResponse:
                                    message = "Your deactivation request did not reach the server. Check your internet connection and try again.";
                                    title = "No Response Received";
                                    break;
                                }

                                m_mainWindow.ShowUserMessage(title, message);
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
                            ModelManager.Get<HistoryViewModel>()?.AppendBlockActionEvent(args.Category, args.Resource.ToString(), args.BlockDate);
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
                    // We don't have to do anything here on our side, but we may want to do something
                    // here in the future if we modify how our UI shows relaxed policy timer stuff.
                    // Like perhaps changing views etc.
                };

                m_ipcClient.RelaxedPolicyInfoReceived = (args) =>
                {
                    Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Normal,
                        (Action)delegate ()
                        {
                            // Bypass lists have been enabled.
                            var rpModel = ModelManager.Get<RelaxedPolicyViewModel>();
                            if(args.PolicyInfo != null && rpModel != null)
                            {
                                rpModel.AvailableRelaxedRequests = args.PolicyInfo.NumberAvailableToday;
                                rpModel.RelaxedDuration = new DateTime(args.PolicyInfo.RelaxDuration.Ticks).ToString("HH:mm");

                                switch(args.PolicyInfo.Status)
                                {
                                    case RelaxedPolicyStatus.Activated:
                                    case RelaxedPolicyStatus.Granted:
                                        rpModel.IsRelaxedPolicyInEffect = true;
                                        break;

                                    default:
                                        rpModel.IsRelaxedPolicyInEffect = false;
                                        break;
                                }

                                // Ensure we don't overlap this event multiple times by decrementing first.
                                rpModel.RelaxedPolicyRequested -= OnRelaxedPolicyRequested;
                                rpModel.RelaxedPolicyRequested += OnRelaxedPolicyRequested;

                                // Ensure we don't overlap this event multiple times by decrementing first.
                                rpModel.RelinquishRelaxedPolicyRequested -= OnRelinquishRelaxedPolicyRequested;
                                rpModel.RelinquishRelaxedPolicyRequested += OnRelinquishRelaxedPolicyRequested;
                            }
                        }
                    );
                };

                m_ipcClient.StateChanged = (args) =>
                {
                    m_logger.Info("Filter status from server is: {0}", args.State.ToString());
                    m_hasStateBeenFetched = true;

                    switch(args.State)
                    {
                        case FilterStatus.CooldownPeriodEnforced:
                            {
                                // Add this blocked request to the dashboard.
                                Current.Dispatcher.BeginInvoke(
                                    System.Windows.Threading.DispatcherPriority.Normal,
                                    (Action)delegate ()
                                    {
                                        viewManager.Get<RelaxedPolicyView>()?.ShowDisabledInternetMessage(DateTime.Now.Add(args.CooldownPeriod));
                                    }
                                );
                            }
                            break;

                        case FilterStatus.Running:
                            {
                                viewManager.PopView(typeof(ProgressWait));

                                // Change UI state of dashboard to not show disabled message anymore.
                                // If we're not already in a disabled state, this will have no effect.
                                Current.Dispatcher.BeginInvoke(
                                    System.Windows.Threading.DispatcherPriority.Normal,
                                    (Action)delegate ()
                                    {
                                        viewManager.Get<RelaxedPolicyView>()?.HideDisabledInternetMessage();
                                    }
                                );
                            }
                            break;

                        case FilterStatus.Synchronizing:
                            {
                                // Update our timestamps for last sync.
                                viewManager.PushView(ProgressWait.ModalZIndex, typeof(ProgressWait));

                                lock(m_synchronizingTimerLockObj)
                                {
                                    if(m_synchronizingTimer != null)
                                    {
                                        m_synchronizingTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                                        m_synchronizingTimer.Dispose();
                                        m_synchronizingTimer = null;
                                    }

                                    m_synchronizingTimer = new Timer((state) =>
                                    {
                                        m_ipcClient.RequestStatusRefresh();
                                        m_synchronizingTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                                        m_synchronizingTimer.Dispose();
                                        m_synchronizingTimer = null;
                                    });

                                    m_synchronizingTimer.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
                                }
                            }
                            break;

                        case FilterStatus.Synchronized:
                            {
                                // Update our timestamps for last sync.
                                viewManager.PopView(typeof(ProgressWait));

                                // Change UI state of dashboard to not show disabled message anymore.
                                // If we're not already in a disabled state, this will have no effect.
                                Current.Dispatcher.BeginInvoke(
                                    System.Windows.Threading.DispatcherPriority.Normal,
                                    (Action)delegate ()
                                    {
                                        var relaxedPolicyViewModel = ModelManager.Get<RelaxedPolicyViewModel>();
                                        if(relaxedPolicyViewModel != null)
                                        {
                                            relaxedPolicyViewModel.LastSync = DateTime.Now;
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

                // TODO: Add helper functions to wrap code in dispatcher functions.
                // TODO: Add helper functions to bypass the need for any message handling.
                m_ipcClient.RegisterResponseHandler<bool?>(IpcCall.TimeRestrictionsEnabled, (msg) =>
                {
                    bool? areTimeRestrictionsEnabled = msg.Data;

                    m_mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        var viewModel = ModelManager.Get<TimeRestrictionsViewModel>();

                        viewModel.AreTimeRestrictionsActive = areTimeRestrictionsEnabled;
                    });
                    
                    return true;
                });

                m_ipcClient.RegisterResponseHandler<AppConfigModel>(IpcCall.ConfigurationInfo, (msg) =>
                {
                    var cfg = msg.Data;
                    m_appConfig = cfg;

                    m_mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        var viewModel = ModelManager.Get<SelfModerationViewModel>();

                        viewModel.SelfModerationSites.Clear();

                        foreach (string site in cfg.SelfModeration)
                        {
                            viewModel.SelfModerationSites.Add(site);
                        }

                        viewModel.TriggerBlacklist.Clear();

                        foreach (string site in cfg.CustomTriggerBlacklist)
                        {
                            viewModel.TriggerBlacklist.Add(site);
                        }
                    });

                    m_mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        var viewModel = ModelManager.Get<TimeRestrictionsViewModel>();

                        viewModel.UpdateRestrictions(cfg.TimeRestrictions);
                    });

                    return true;
                });

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

            Application.Current.Shutdown((int)ExitCodes.ShutdownWithSafeguards);

            // Does this cause a hand up?? m_ipcClient.Dispose();
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
            m_mainWindow = new Te.Citadel.UI.Windows.MainWindow();

            ModelManager = new ModelManager();
            viewManager = new ViewManager(m_mainWindow);

            ModelManager.Register(new HistoryViewModel());
            ModelManager.Register(new SelfModerationViewModel());
            ModelManager.Register(new RelaxedPolicyViewModel());
            ModelManager.Register(new AdvancedViewModel());
            ModelManager.Register(new DiagnosticsViewModel());
            ModelManager.Register(new TimeRestrictionsViewModel());
            ModelManager.Register(new SupportViewModel());
            ModelManager.Register(new CollectDiagnosticsViewModel());

            viewManager.Register(new DashboardView());
            viewManager.Register(new LoginView());
            viewManager.Register(new ProgressWait());
            viewManager.Register(new CollectDiagnosticsView());

            m_mainWindow.WindowRestoreRequested += (() =>
            {
                BringAppToFocus();
            });

            m_mainWindow.Closing += ((object sender, CancelEventArgs e) =>
            {
                // Don't actually let the window close, just hide it.
                e.Cancel = true;

                if(m_mainWindow.CurrentView.Content is LoginView)
                {
                    Application.Current.Shutdown((int)ExitCodes.ShutdownWithoutSafeguards);
                    return;
                }

                // When the main window closes, go to tray and show notification.
                MinimizeToTray(true);
            });

            BaseCitadelViewModel dashboardVm = ModelManager.Get<DashboardViewModel>();
            if(dashboardVm != null)
            {
                dashboardVm.UserNotificationRequest = OnNotifyUserRequest;
            }

            // Set the current view to ProgressWait because we're gonna do background init next.
            this.MainWindow = m_mainWindow;
            m_mainWindow.Show();

            viewManager.SetBaseView(typeof(DashboardView));
            viewManager.PushView(ProgressWait.ModalZIndex, typeof(ProgressWait));
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
                    // Give us a nice long minute to restart. If the user restarts us manually in the
                    // meantime who cares we have a global mutex preventing multiple instance and
                    // this scheduled startup will just not run.
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
            menuItems.Add(new System.Windows.Forms.MenuItem("Block History", TrayIcon_OpenBlockHistory));
            menuItems.Add(new System.Windows.Forms.MenuItem("Use Relaxed Policy", TrayIcon_UseRelaxedPolicy));

            m_trayIcon.ContextMenu = new System.Windows.Forms.ContextMenu(menuItems.ToArray());
        }

        private void TrayIcon_Open(object sender, EventArgs e)
        {
            BringAppToFocus();
        }

        private void TrayIcon_OpenBlockHistory(object sender, EventArgs e)
        {
            BringAppToFocus();
            viewManager.Get<DashboardView>()?.SwitchTab(1);
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

            System.Diagnostics.Process.Start(CloudVeil.CompileSecrets.ConnectivityCheck);
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

        private void OnRelaxedPolicyRequested()
        {
            OnRelaxedPolicyRequested(false);
        }

        public void BeginUpdateRequest(ApplicationUpdate update)
        {
            var updateAvailableString = string.Format("An update to version {0} is available. You are currently running version {1}. Would you like to update now?", update.UpdateVersion.ToString(), update.CurrentVersion.ToString());

            if (update.IsRestartRequired)
            {
                updateAvailableString += "\r\n\r\nThis update WILL require a reboot. Save all your work before continuing.";
            }

            Current.Dispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Normal,

                (Action)async delegate ()
                {
                    if (m_mainWindow != null)
                    {
                        var result = await m_mainWindow.AskUserUpdateQuestion("Update Available", updateAvailableString);
                        m_ipcClient.Send<UpdateDialogResult>(IpcCall.UpdateResult, result);
                    }
                });
        }

        private void ShowRelaxedPolicyMessage(RelaxedPolicyMessage msg, bool fromTray)
        {
            string title = "Relaxed Policy";
            string message = "";

            switch (msg.PolicyInfo.Status)
            {
                case RelaxedPolicyStatus.Activated:
                    message = msg.Message;
                    break;

                case RelaxedPolicyStatus.Granted:
                    message = "Relaxed Policy Granted";
                    break;

                case RelaxedPolicyStatus.AllUsed:
                    message = "All of your relaxed policies are used up for today.";
                    break;

                case RelaxedPolicyStatus.AlreadyRelinquished:
                    message = "Relaxed policy not currently active.";
                    break;

                case RelaxedPolicyStatus.Unauthorized:
                    message = "You entered an incorrect relaxed policy passcode.";
                    break;

                case RelaxedPolicyStatus.Deactivated:
                    message = null;
                    break;

                case RelaxedPolicyStatus.Relinquished:
                    message = null;
                    break;

                default:
                    message = null;
                    break;
            }

            if (fromTray)
            {
                if (message != null)
                {
                    m_trayIcon.ShowBalloonTip(3000, title, message, System.Windows.Forms.ToolTipIcon.Info);
                }
            }
            else
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (message != null)
                    {
                        m_mainWindow.ShowUserMessage(title, message, "OK");
                    }
                });
            }
        }

        /// <summary>
        /// Called whenever a relaxed policy has been requested. 
        /// </summary>
        private async void OnRelaxedPolicyRequested(bool fromTray)
        {
            string passcode = null;
            if(m_appConfig != null && m_appConfig.EnableRelaxedPolicyPasscode)
            {
                if(fromTray)
                {
                    BringAppToFocus();
                }

                passcode = await m_mainWindow.PromptUser("Enter Passcode", "The relaxed policy passcode restriction is enabled. To continue enabling relaxed policy, please enter your passcode.");

                if(fromTray)
                {
                    MinimizeToTray(false);
                }
            }

            using(var ipcClient = new IPCClient())
            {
                ipcClient.ConnectedToServer = () =>
                {
                    ipcClient.RequestRelaxedPolicy(passcode);
                };

                ipcClient.RelaxedPolicyInfoReceived += delegate (RelaxedPolicyMessage msg)
                {
                    ShowRelaxedPolicyMessage(msg, fromTray);
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

                ipcClient.RelaxedPolicyInfoReceived += delegate (RelaxedPolicyMessage msg)
                {
                    ShowRelaxedPolicyMessage(msg, false);
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