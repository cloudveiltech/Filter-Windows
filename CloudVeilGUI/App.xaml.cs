/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Filter.Platform.Common.Extensions;
using CloudVeil.Core.Windows.Util;
using CloudVeil.IPC;
using CloudVeil.IPC.Messages;
using Filter.Platform.Common.Util;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Gui.CloudVeil.UI;
using Gui.CloudVeil.UI.ViewModels;
using Gui.CloudVeil.UI.Views;
using Gui.CloudVeil.UI.Windows;
using Gui.CloudVeil.Util;
using Filter.Platform.Common;
using Filter.Platform.Common.Client;
using Filter.Platform.Common.Data.Models;
using CloudVeil.Core.Windows.Util.Update;
using Filter.Platform.Common.Types;
using Filter.Platform.Common.IPC.Messages;
using FilterNativeWindows;
using Gui.CloudVeil;
using MahApps.Metro.Controls.Dialogs;
using CloudVeil.Core.Windows.Services;
using System.Runtime.InteropServices;

namespace CloudVeil.Windows
{
    /// <summary>
    /// Interaction logic for App.xaml 
    /// </summary>
    public partial class CloudVeilApp : Application
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
        private ReaderWriterLockSlim runAtStartupLock = new ReaderWriterLockSlim();

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
        /// Used to force-query the server whenever we're told to go into a synchronize-wait state. 
        /// </summary>
        private Timer synchronizingTimer;

        private object synchronizingTimerLockObj = new object();

        /// <summary>
        /// Used to track whether we should allow view changes away from ProgressWait when filter state has not yet been fetched.
        /// </summary>
        private bool hasStateBeenFetched = false;

        private AppConfigModel appConfig = null;

        /// <summary>
        /// Logger. 
        /// </summary>
        private readonly Logger logger;

        /// <summary>
        /// Shown when the program is minimized to the tray. The app is always minimized to the tray
        /// on close.
        /// </summary>
        private System.Windows.Forms.NotifyIcon trayIcon;

        /// <summary>
        /// This BackgroundWorker object handles initializing the application off the UI thread.
        /// Allows the splash screen to function.
        /// </summary>
        private BackgroundWorker backgroundInitWorker;

        /// <summary>
        /// Primary and only window we use. 
        /// </summary>
        private MainWindow mainWindow;

        /// <summary>
        /// Used to ensure synchronized access when setting DNS settings. 
        /// </summary>
        private object dnsEnforcementLock = new object();

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
        private IPCClient ipcClient;

        public IPCClient IpcClient => ipcClient;
        #endregion Views

        public Dictionary<string, CertificateExemptionMessage> SslExemptions { get; set; }

        /// <summary>
        /// Default ctor. 
        /// </summary>
        public CloudVeilApp()
        {
            logger = LoggerUtil.GetAppWideLogger();

            string appVerStr = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            appVerStr += " " + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString(3);
            appVerStr += " " + RuntimeInformation.ProcessArchitecture.ToString();

            logger.Info("CloudVeilGUI Version: {0}", appVerStr);

            // Enforce good/proper protocols
            ServicePointManager.SecurityProtocol = (ServicePointManager.SecurityProtocol & ~SecurityProtocolType.Ssl3) | (SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12);

            this.Startup += CloudVeilOnStartup;
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

        private void CloudVeilOnStartup(object sender, StartupEventArgs e)
        {
            CloudVeil.Core.Windows.Platform.Init();

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
                LoggerUtil.RecursivelyLogException(logger, ve);
            }

            try
            {
                // XXX FIXME
                ipcClient = IPCClient.InitDefault();
                ipcClient.AuthenticationResultReceived = (authenticationFailureResult) =>
                {
                    switch(authenticationFailureResult.Action)
                    {
                        case AuthenticationAction.Denied:
                        case AuthenticationAction.Required:
                        case AuthenticationAction.InvalidInput:
                            {
                                // User needs to log in.
                                BringAppToFocus();

                                mainWindow.Dispatcher.InvokeAsync(() =>
                                {
                                    ((MainWindowViewModel)mainWindow.DataContext).IsUserLoggedIn = false;
                                });

                                viewManager.PushView(LoginView.ModalZIndex, typeof(LoginView));
                            }
                            break;

                        case AuthenticationAction.Authenticated:
                        case AuthenticationAction.ErrorNoInternet:
                        case AuthenticationAction.ErrorUnknown:
                            {
                                logger.Info($"The logged in user is {authenticationFailureResult.Username}");

                                mainWindow.Dispatcher.InvokeAsync(() =>
                                {
                                    ((MainWindowViewModel)mainWindow.DataContext).LoggedInUser = authenticationFailureResult.Username;
                                    ((MainWindowViewModel)mainWindow.DataContext).IsUserLoggedIn = true;
                                });

                                // This code prevents the progress->dashboard->progress flash for authenticated users, but not for error'd users.
                                if (authenticationFailureResult.Action != AuthenticationAction.Authenticated)
                                {
                                    viewManager.PopView(typeof(LoginView));
                                }
                                else if (hasStateBeenFetched)
                                {
                                    viewManager.PopView(typeof(LoginView));
                                }
                            }
                            break;
                    }
                };

                ipcClient.RegisterResponseHandler<ConfigCheckInfo>(IpcCall.SynchronizeSettings, (msg) =>
                {
                    var vm = ModelManager.Get<AdvancedViewModel>();
                    vm.OnSettingsSynchronized(msg);
                    return true;
                });

                ipcClient.RegisterResponseHandler<UpdateCheckInfo>(IpcCall.CheckForUpdates, (msg) =>
                {
                    var vm = ModelManager.Get<AdvancedViewModel>();
                    vm.OnCheckForUpdates(msg);
                    return true;
                });

                ipcClient.RegisterResponseHandler<ApplicationUpdate>(IpcCall.Update, (msg) =>
                {
                    var vm = ModelManager.Get<AdvancedViewModel>();

                    if(msg.Data.CurrentVersion >= msg.Data.UpdateVersion)
                    {
                        return true;
                    }

                    this.BeginUpdateRequest(msg.Data);

                    return true;
                });

                ipcClient.RegisterResponseHandler<object>(IpcCall.InstallerDownloadStarted, (msg) =>
                {
                    mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        mainWindow.ViewModel.DownloadFlyoutIsOpen = true;
                        mainWindow.ViewModel.DownloadProgress = 0;
                    });

                    return true;
                });

                ipcClient.RegisterResponseHandler<int>(IpcCall.InstallerDownloadProgress, (msg) =>
                {
                    mainWindow.Dispatcher.InvokeAsync(() => mainWindow.ViewModel.DownloadProgress = msg.Data);

                    return true;
                });

                ipcClient.RegisterResponseHandler<bool>(IpcCall.InstallerDownloadFinished, (msg) =>
                {
                    if (msg.Data)
                    {
                        mainWindow.Dispatcher.InvokeAsync(() => mainWindow.ViewModel.DownloadProgress = 100);

                        Task.Run(async () =>
                        {
                            await Task.Delay(3000);
                            await mainWindow.Dispatcher.InvokeAsync(() => mainWindow.ViewModel.DownloadFlyoutIsOpen = false);
                        });
                    }
                    else
                    {
                        mainWindow.Dispatcher.InvokeAsync(() => mainWindow.ShowUserMessage("Update Failed", "Failed to download the update file."));
                    }

                    return true;
                });

                ipcClient.RegisterRequestHandler(IpcCall.ShutdownForUpdate, (msg) =>
                {
                    Application.Current.Shutdown((int)ExitCodes.ShutdownForUpdate);
                    return true;
                });

                ipcClient.RegisterResponseHandler<List<ConflictReason>>(IpcCall.ConflictsDetected, (msg) =>
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

                        mainWindow.Dispatcher.InvokeAsync(() =>
                        {
                            mainWindow.ViewModel.ConflictReasons.Clear();
                            
                            foreach(var conflict in conflicts)
                            {
                                mainWindow.ViewModel.ConflictReasons.Add(conflict);
                            }
                        });
                    }
                    else
                    {
                        mainWindow.Dispatcher.InvokeAsync(() => mainWindow.ViewModel.ConflictReasons.Clear());
                    }

                    return true;
                });

                ipcClient.RegisterResponseHandler<string>(IpcCall.ActivationIdentifier, (msg) =>
                {
                    {
                        var vm = ModelManager.Get<SupportViewModel>();
                        mainWindow.Dispatcher.Invoke(() => vm.ActivationIdentifier = msg.Data);
                    }

                    {
                        var vm = ModelManager.Get<SelfModerationViewModel>();
                        mainWindow.Dispatcher.Invoke(() => vm.ActivationIdentifier = msg.Data);
                    }

                    return true;
                });

                ipcClient.DeactivationResultReceived = (deactivationCmd) =>
                {
                    logger.Info("Deactivation command is: {0}", deactivationCmd.ToString());

                    if(deactivationCmd == DeactivationCommand.Granted)
                    {
                        if(CriticalKernelProcessUtility.IsMyProcessKernelCritical)
                        {
                            CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();
                        }

                        logger.Info("Deactivation request granted on client.");

                        // Init the shutdown of this application.
                        Application.Current.Shutdown((int)ExitCodes.ShutdownWithoutSafeguards);
                    }
                    else
                    {
                        logger.Info("Deactivation request denied on client.");

                        Current.Dispatcher.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Normal,
                        (Action)delegate ()
                        {
                            if(mainWindow != null)
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

                                mainWindow.ShowUserMessage(title, message);
                            }
                        }
                    );
                    }
                };

                ipcClient.BlockActionReceived = (args) =>
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

                ipcClient.ConnectedToServer = () =>
                {
                    logger.Info("Connected to IPC server.");
                };

                ipcClient.DisconnectedFromServer = () =>
                {
                    logger.Warn("Disconnected from IPC server! Automatically attempting reconnect.");
                };

                ipcClient.RelaxedPolicyExpired = () =>
                {
                    // We don't have to do anything here on our side, but we may want to do something
                    // here in the future if we modify how our UI shows relaxed policy timer stuff.
                    // Like perhaps changing views etc.
                };

                ipcClient.RelaxedPolicyInfoReceived = (args) =>
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

                                long relaxDurationTicks = args.PolicyInfo.RelaxDuration.Ticks;
                                if (relaxDurationTicks < DateTime.MinValue.Ticks || relaxDurationTicks > DateTime.MaxValue.Ticks)
                                {
                                    rpModel.RelaxedDuration = "(n/a)";
                                }
                                else
                                {
                                    rpModel.RelaxedDuration = new DateTime(relaxDurationTicks).ToString("HH:mm");
                                }

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

                ipcClient.StateChanged = (args) =>
                {
                    logger.Info("Filter status from server is: {0}", args.State.ToString());
                    hasStateBeenFetched = true;

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

                                lock(synchronizingTimerLockObj)
                                {
                                    if(synchronizingTimer != null)
                                    {
                                        synchronizingTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                                        synchronizingTimer.Dispose();
                                        synchronizingTimer = null;
                                    }

                                    synchronizingTimer = new Timer((state) =>
                                    {
                                        ipcClient.RequestStatusRefresh();
                                        synchronizingTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                                        synchronizingTimer.Dispose();
                                        synchronizingTimer = null;
                                    });

                                    synchronizingTimer.Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
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

                ipcClient.ClientToClientCommandReceived = (args) =>
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
                ipcClient.RegisterResponseHandler<bool?>(IpcCall.TimeRestrictionsEnabled, (msg) =>
                {
                    bool? areTimeRestrictionsEnabled = msg.Data;

                    mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        var viewModel = ModelManager.Get<TimeRestrictionsViewModel>();

                        viewModel.AreTimeRestrictionsActive = areTimeRestrictionsEnabled;
                    });
                    
                    return true;
                });

                ipcClient.RegisterResponseHandler<AppConfigModel>(IpcCall.ConfigurationInfo, (msg) =>
                {
                    var cfg = msg.Data;
                    appConfig = cfg;

                    mainWindow.Dispatcher.InvokeAsync(() =>
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

                        var diagnosticsViewModel = ModelManager.Get<DiagnosticsViewModel>();
                        diagnosticsViewModel.IsDnsEnforcementEnabled = cfg.PrimaryDns.Length != 0 || cfg.SecondaryDns.Length != 0;


                        var advancedViewModel = ModelManager.Get<AdvancedViewModel>();
                        advancedViewModel.FriendlyName = cfg.FriendlyName;
                    });

                    mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        var viewModel = ModelManager.Get<TimeRestrictionsViewModel>();

                        viewModel.UpdateRestrictions(cfg.TimeRestrictions);
                    });

                    return true;
                });


                ipcClient.RegisterResponseHandler<ushort[]>(IpcCall.PortsValue, (msg) =>
                {
                    var advancedViewModel = ModelManager.Get<AdvancedViewModel>();
                    var data = msg.Data;
                    advancedViewModel.Ports = data;
                    return true;
                });

                ipcClient.RegisterResponseHandler<bool>(IpcCall.RandomizePortsValue, (msg) =>
                {
                    var advancedViewModel = ModelManager.Get<AdvancedViewModel>();
                    var data = msg.Data;
                    advancedViewModel.IsPortsRandomized = data;
                    return true;
                });

                ipcClient.RegisterResponseHandler<BugReportSetting>(IpcCall.BugReportConfirmationValue, (msg) =>
                {
                    var advancedViewModel = ModelManager.Get<AdvancedViewModel>();
                    var data = msg.Data ?? new BugReportSetting(false, false);
                    advancedViewModel.BugReportSettings = data;
                    if(!data.DialogShown)
                    {
                        Current.Dispatcher.Invoke(
                            System.Windows.Threading.DispatcherPriority.Normal,

                            (Action)async delegate ()
                            {
                                if (mainWindow != null)
                                {
                                    var result = await mainWindow.AskUser("Bug report", "Do you want to share bug reports with CloudVeil?");
                                    var newSettings = new BugReportSetting(result, true);
                                    ipcClient.Send<BugReportSetting>(IpcCall.BugReportConfirmationValue, newSettings);
                                    advancedViewModel.BugReportSettings = newSettings;

                                    if (data.Allowed != result)
                                    {
                                        SwitchSentry(result);
                                    }
                                }
                        });
                        return true;
                    }
                    SwitchSentry(data.Allowed && data.DialogShown);
                    return true;
                });


                ipcClient.CaptivePortalDetectionReceived = (msg) =>
                {
                    // C# doesn't like cross-thread GUI variable access, so run this on window thread.
                    mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        ((MainWindowViewModel)mainWindow.DataContext).ShowIsGuestNetwork = msg.IsCaptivePortalDetected;
                    });
                };

#if CAPTIVE_PORTAL_GUI_ENABLED
                ipcClient.CaptivePortalDetectionReceived = (msg) =>
                {
                    if (msg.IsCaptivePortalDetected && !captivePortalShownToUser)
                    {
                        if (mainWindow.Visibility == Visibility.Visible)
                        {
                            if (!mainWindow.IsVisible)
                            {
                                BringAppToFocus();
                            }

                            ((MainWindowViewModel)mainWindow.DataContext).ShowIsGuestNetwork = true;
                        }
                        else
                        {
                            DisplayCaptivePortalToolTip();
                        }

                        captivePortalShownToUser = true;
                    }
                    else if(!msg.IsCaptivePortalDetected)
                    {
                        captivePortalShownToUser = false;
                    }
                };
#endif
            }
            catch(Exception ipce)
            {
                LoggerUtil.RecursivelyLogException(logger, ipce);
            }

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // Run the background init worker for non-UI related initialization.
            backgroundInitWorker = new BackgroundWorker();
            backgroundInitWorker.DoWork += DoBackgroundInit;
            backgroundInitWorker.RunWorkerCompleted += OnBackgroundInitComplete;

            backgroundInitWorker.RunWorkerAsync(e);
        }

        private void SwitchSentry(bool enabled)
        {
            if (enabled)
            {
                CloudVeilGuiMain.StartSentry();
            }
            else
            {
                CloudVeilGuiMain.StopSentry();
            }
        }

        private void ScheduleAppRestart(int secondDelay = 30)
        {
            logger.Info("Scheduling GUI restart {0} seconds from now.", secondDelay);

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
            logger.Info("Session ending.");

            // THIS MUST BE DONE HERE ALWAYS, otherwise, we get BSOD.
            CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();

            Application.Current.Shutdown((int)ExitCodes.ShutdownWithSafeguards);

            // Does this cause a hand up?? ipcClient.Dispose();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception err = e.ExceptionObject as Exception;
            LoggerUtil.RecursivelyLogException(logger, err);
        }

        /// <summary>
        /// Called to initialize the various application views on startup. 
        /// </summary>
        private void InitViews()
        {
            mainWindow = new Gui.CloudVeil.UI.Windows.MainWindow();

            ModelManager = new ModelManager();
            viewManager = new ViewManager(mainWindow);

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

            mainWindow.WindowRestoreRequested += (() =>
            {
        //        BringAppToFocus();
            });

            mainWindow.Closing += ((object sender, CancelEventArgs e) =>
            {
                // Don't actually let the window close, just hide it.
                e.Cancel = true;

                if(mainWindow.CurrentView.Content is LoginView)
                {
                    Application.Current.Shutdown((int)ExitCodes.ShutdownWithoutSafeguards);
                    return;
                }

                // When the main window closes, go to tray and show notification.
                MinimizeToTray(true);         
            });

            BaseCloudVeilViewModel dashboardVm = ModelManager.Get<DashboardViewModel>();
            if(dashboardVm != null)
            {
                dashboardVm.UserNotificationRequest = OnNotifyUserRequest;
            }

            // Set the current view to ProgressWait because we're gonna do background init next.
            this.MainWindow = mainWindow;
            mainWindow.Show();

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
                logger.Info("Application shutdown detected with code {0}.", e.ApplicationExitCode);

                // Unhook first.
                this.Exit -= OnApplicationExiting;
            }
            catch(Exception err)
            {
                LoggerUtil.RecursivelyLogException(logger, err);
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
            WFPUtility.EnableInternet();

            if(e.Cancelled || e.Error != null)
            {
                logger.Error("Error during initialization.");
                if(e.Error != null && logger != null)
                {
                    LoggerUtil.RecursivelyLogException(logger, e.Error);
                }

                Current.Shutdown(-1);
                return;
            }
        }

        /// <summary>
        /// Initializes the trayIcon member, loading the icon graphic and hooking appropriate
        /// handlers to respond to user iteraction requesting to bring the application back out of
        /// the tray.
        /// </summary>
        private void InitTrayIcon()
        {
            trayIcon = new System.Windows.Forms.NotifyIcon();

            var iconPackUri = new Uri("pack://application:,,,/Resources/appicon.ico");
            var resourceStream = GetResourceStream(iconPackUri);

            trayIcon.Icon = new System.Drawing.Icon(resourceStream.Stream);

            trayIcon.DoubleClick +=
                delegate (object sender, EventArgs args)
                {
                    BringAppToFocus();
                };

            trayIcon.BalloonTipClosed += delegate (object sender, EventArgs args)
            {
                // Windows 10 looks like it likes to hide tray icon when a user clicks on a tool tip.
                // Force it to stay visible.
                trayIcon.Visible = true;
            };

            trayIcon.BalloonTipClicked += bringAppToFromBallonClicked;

            var menuItems = new List<System.Windows.Forms.MenuItem>();
            menuItems.Add(new System.Windows.Forms.MenuItem("Open", TrayIcon_Open));
            menuItems.Add(new System.Windows.Forms.MenuItem("Block History", TrayIcon_OpenBlockHistory));
            menuItems.Add(new System.Windows.Forms.MenuItem("Use Relaxed Policy", TrayIcon_UseRelaxedPolicy));
            menuItems.Add(new System.Windows.Forms.MenuItem("Sync Settings", TrayIcon_Sync));

            trayIcon.ContextMenu = new System.Windows.Forms.ContextMenu(menuItems.ToArray());
        }

        private void TrayIcon_Open(object sender, EventArgs e)
        {
            BringAppToFocus();
        }

        private void TrayIcon_OpenBlockHistory(object sender, EventArgs e)
        {
            BringAppToFocus();
            viewManager.Get<DashboardView>()?.SwitchTab(typeof(HistoryView));
        }

        private void TrayIcon_Sync(object sender, EventArgs e)
        {
            BringAppToFocus();
            viewManager.Get<DashboardView>()?.SwitchTab(typeof(AdvancedView));
            ModelManager.Get<AdvancedViewModel>().SyncSettingsCommand.Execute(null);
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
                    if(this.mainWindow != null)
                    {
                        this.mainWindow.Show();
                        this.mainWindow.WindowState = WindowState.Normal;
                        this.mainWindow.Topmost = true;
                        this.mainWindow.Topmost = false;
                    }

                    if(trayIcon != null)
                    {
                        trayIcon.Visible = false;
                    }
                }
            );
        }

        private void bringAppToFromBallonClicked(object sender, EventArgs e)
        {
            BringAppToFocus();
        }

#if CAPTIVE_PORTAL_GUI_ENABLED
        public void DisplayCaptivePortalToolTip()
        {
            trayIcon.BalloonTipClicked += captivePortalToolTipClicked;
            trayIcon.ShowBalloonTip(6000, "Captive Portal Detected", "This network requires logon information. Click here to continue.", System.Windows.Forms.ToolTipIcon.Info);
        }
#endif

        private void captivePortalToolTipClicked(object sender, EventArgs e)
        {
            trayIcon.BalloonTipClicked -= captivePortalToolTipClicked;

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
            mainWindow.ShowUserMessage(title, message);
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
                    if (mainWindow != null)
                    {
                        if(trayIcon != null)
                        {
                            trayIcon.ShowBalloonTip(3000, "Update Available", updateAvailableString, System.Windows.Forms.ToolTipIcon.Info);
                        }
                        var result = await mainWindow.AskUserUpdateQuestion("Update Available", updateAvailableString);
                        ipcClient.Send<UpdateDialogResult>(IpcCall.UpdateResult, result);
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
                    trayIcon.ShowBalloonTip(3000, title, message, System.Windows.Forms.ToolTipIcon.Info);
                }
            }
            else
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (message != null)
                    {
                        mainWindow.ShowUserMessage(title, message, "OK");
                    }
                });
            }
        }

        /// <summary>
        /// Called whenever a relaxed policy has been requested. 
        /// </summary>
        private async void OnRelaxedPolicyRequested(bool fromTray)
        {
            LoginDialogData passcodeData = null;
            string passcode = "";
            if(appConfig != null && appConfig.EnableRelaxedPolicyPasscode)
            {
                if(fromTray)
                {
                    BringAppToFocus();
                }

                passcodeData = await mainWindow.PromptUserForPassword("Enter Passcode", "The relaxed policy passcode restriction is enabled. To continue enabling relaxed policy, please enter your passcode.");
                if(passcodeData != null)
                {
                    passcode = passcodeData.Password;
                }

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
                    if(mainWindow != null && trayIcon != null)
                    {
                        trayIcon.Visible = true;
                        mainWindow.Visibility = Visibility.Hidden;

                        if(showTip)
                        {
                            trayIcon.ShowBalloonTip(1500, "Still Running", string.Format("{0} will continue running in the background.", Process.GetCurrentProcess().ProcessName), System.Windows.Forms.ToolTipIcon.Info);
                        }

                    }
                }
            );
        }

        // XXX FIXME
        private void DoCleanShutdown()
        {
            lock(cleanShutdownLock)
            {
                if(!cleanShutdownComplete)
                {
                    ipcClient.Dispose();

                    try
                    {
                        // Pull our icon from the task tray.
                        if(trayIcon != null)
                        {
                            trayIcon.Visible = false;
                        }
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
                    }

                    try
                    {
                        // Pull our critical status.
                        CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
                    }

                    // Flag that clean shutdown was completed already.
                    cleanShutdownComplete = true;
                }
            }
        }
    }
}