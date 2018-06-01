/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Types;
using Citadel.Core.Windows.Util;
using Citadel.IPC;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Te.Citadel.Extensions;
using Te.Citadel.Testing;
using Te.Citadel.UI.Models;
using Te.Citadel.UI.Views;
using Te.Citadel.Util;

namespace Te.Citadel.UI.ViewModels
{ 

    /// <summary>
    /// Class for displaying block event information in a DataGrid.
    /// </summary>
    public class ViewableBlockedRequests : ObservableObject
    {
        public string CategoryName
        {
            get;
            private set;
        }

        public string FullRequest
        {
            get;
            private set;
        }

        public ViewableBlockedRequests(string category, string fullRequest)
        {
            this.CategoryName = category;
            this.FullRequest = fullRequest;
        }
    }

    public class DashboardViewModel : BaseCitadelViewModel
    {

        /// <summary>
        /// The model.
        /// </summary>
        private DashboardModel m_model = new DashboardModel();

        /// <summary>
        /// List of observable block actions that the user can view.
        /// </summary>
        public ObservableCollection<ViewableBlockedRequests> BlockEvents
        {
            get;
            set;
        }

        public DashboardViewModel()
        {
            BlockEvents = new ObservableCollection<ViewableBlockedRequests>();
            DiagnosticsEntries = new ObservableCollection<DiagnosticsEntryViewModel>();
        }

        /// <summary>
        /// Private data member for the public DeactivateCommand property.
        /// </summary>
        private RelayCommand m_deactivationCommand;

        /// <summary>
        /// Private data member for the public RequestBlockActionReviewCommand property.
        /// </summary>
        private RelayCommand<ViewableBlockedRequests> m_requestBlockActionReviewCommand;

        /// <summary>
        /// Private data member for the public ViewLogsCommand property.
        /// </summary>
        private RelayCommand m_viewLogsCommand;

        /// <summary>
        /// Private data member for the public UseRelaxedPolicyCommand property.
        /// </summary>
        private RelayCommand m_useRelaxedPolicyCommand;

        /// <summary>
        /// Private data member for the public RelinquishRelaxedPolicyCommand property.
        /// </summary>
        private RelayCommand m_relinquishRelaxedPolicyCommand;

        private RelayCommand m_viewSslExemptionsCommand;

        internal DashboardModel Model
        {
            get
            {
                return m_model;
            }
        }

        public RelayCommand ViewSslExemptionsCommand
        {
            get
            {
                if(m_viewSslExemptionsCommand == null)
                {
                    m_viewSslExemptionsCommand = new RelayCommand((Action)(() =>
                    {
                        ViewChangeRequest?.Invoke(typeof(SslExemptionsView));
                    }));
                }

                return m_viewSslExemptionsCommand;
            }
        }

        /// <summary>
        /// Command to run a deactivation request for the current authenticated user.
        /// </summary>
        public RelayCommand RequestDeactivateCommand
        {
            get
            {
                if(m_deactivationCommand == null)
                {
                    m_deactivationCommand = new RelayCommand((Action)(() =>
                    {
                        try
                        {
                            Task.Run(() =>
                            {
                                using(var ipcClient = new IPCClient())
                                {
                                    ipcClient.ConnectedToServer = () =>
                                    {
                                        ipcClient.RequestDeactivation();
                                    };

                                    ipcClient.WaitForConnection();
                                    Task.Delay(3000).Wait();
                                }
                            });
                        }
                        catch(Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }));
                }

                return m_deactivationCommand;
            }
        }

        /// <summary>
        /// Command to request the review of a logged block action.
        /// </summary>
        public RelayCommand<ViewableBlockedRequests> RequestBlockActionReviewCommand
        {
            get
            {
                if(m_deactivationCommand == null)
                {
                    
                    m_requestBlockActionReviewCommand = new RelayCommand<ViewableBlockedRequests>((Action<ViewableBlockedRequests>)((args) =>
                    {
                        string category = args.CategoryName;
                        string fullUrl = args.FullRequest;

                        try
                        {
                            Task.Run(() =>
                            {
                                using(var ipcClient = new IPCClient())
                                {
                                    ipcClient.ConnectedToServer = () =>
                                    {
                                        ipcClient.RequestBlockActionReview(category, fullUrl);
                                    };

                                    ipcClient.WaitForConnection();
                                    Task.Delay(3000).Wait();
                                }
                            });
                        }
                        catch(Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }));
                }

                return m_requestBlockActionReviewCommand;
            }
        }

        private bool m_updateRequestInProgress = false;
        public bool UpdateRequestInProgress
        {
            get { return m_updateRequestInProgress; }
            set
            {
                m_updateRequestInProgress = value;
                RaisePropertyChanged(nameof(UpdateRequestInProgress));
            }
        }

        private bool m_upToDate = false;
        public bool UpToDate
        {
            get { return m_upToDate; }
            set
            {
                m_upToDate = value;
                RaisePropertyChanged(nameof(UpToDate));
            }
        }

        private bool m_errorOccurred = false;
        public bool ErrorOccurred
        {
            get { return m_errorOccurred; }
            set
            {
                m_errorOccurred = value;
                RaisePropertyChanged(nameof(ErrorOccurred));
            }
        }

        private string m_updateText = "Sync";
        public string UpdateText
        {
            get { return m_updateText; }
            set
            {
                m_updateText = value;
                RaisePropertyChanged(nameof(UpdateText));
            }
        }

        private string m_errorText = "";
        public string ErrorText
        {
            get { return m_errorText; }
            set
            {
                m_errorText = value;
                RaisePropertyChanged(nameof(ErrorText));
            }
        }

        private bool m_isUpdateButtonEnabled = true;
        public bool IsUpdateButtonEnabled
        {
            get { return m_isUpdateButtonEnabled; }
            set
            {
                m_isUpdateButtonEnabled = value;
                RaisePropertyChanged(nameof(IsUpdateButtonEnabled));
            }
        }

        private RelayCommand m_requestUpdateCommand;
        public RelayCommand RequestUpdateCommand
        {
            get
            {
                if (m_requestUpdateCommand == null)
                {
                    m_requestUpdateCommand = new RelayCommand(() =>
                    {
                        UpdateRequestInProgress = true;
                        ErrorOccurred = false;
                        ErrorText = "";

                        Task.Run(() =>
                        {
                            using (IPCClient client = new IPCClient())
                            {
                                client.ConnectedToServer = () =>
                                {
                                    client.RequestConfigUpdate((message) =>
                                    {
                                        m_logger.Info("We got a config update message back.");
                                        UpdateRequestInProgress = false;

                                        if (message.UpdateResult.HasFlag(ConfigUpdateResult.AppUpdateAvailable))
                                        {
                                            UpToDate = true;
                                            UpdateText = "New Version";
                                        }
                                        else
                                        {
                                            switch (message.UpdateResult)
                                            {
                                                case ConfigUpdateResult.UpToDate:
                                                    UpToDate = true;
                                                    IsUpdateButtonEnabled = false;
                                                    UpdateText = "Up to date";
                                                    break;

                                                case ConfigUpdateResult.Updated:
                                                    UpToDate = true;
                                                    IsUpdateButtonEnabled = false;
                                                    UpdateText = "Updated";
                                                    break;

                                                case ConfigUpdateResult.NoInternet:
                                                    IsUpdateButtonEnabled = true;
                                                    UpToDate = false;
                                                    ErrorOccurred = true;
                                                    UpdateText = "Try Again";
                                                    ErrorText = "No internet";
                                                    break;

                                                case ConfigUpdateResult.ErrorOccurred:
                                                    IsUpdateButtonEnabled = true;
                                                    UpToDate = false;
                                                    ErrorOccurred = true;
                                                    UpdateText = "Try Again";
                                                    ErrorText = "Error occurred";
                                                    break;

                                                default:
                                                    UpToDate = false;
                                                    ErrorOccurred = true;
                                                    UpdateText = "Try Again";
                                                    ErrorText = "Unrecognized";
                                                    break;
                                            }

                                            var timer = new System.Timers.Timer(30000);
                                            timer.Elapsed += (sender, e) =>
                                            {
                                                IsUpdateButtonEnabled = true;
                                                UpToDate = false;
                                                ErrorOccurred = false;
                                                UpdateText = "Sync";
                                                ErrorText = "";

                                                timer.Dispose();
                                            };
                                            timer.Enabled = true;

                                        }


                                        // TODO: Add code to display on dashboard view model.
                                    });
                                };

                                client.WaitForConnection();
                                Task.Delay(3000).Wait(); // FIXME Surely there's a good way to detect when our work is over with IPCClients
                            }
                        });
                    });
                }

                return m_requestUpdateCommand;
            }
        }

        public RelayCommand ViewLogsCommand
        {
            get
            {
                if(m_viewLogsCommand == null)
                {
                    m_viewLogsCommand = new RelayCommand(() =>
                    {
                        // Scan all Nlog log targets
                        var logDir = string.Empty;

                        var targets = NLog.LogManager.Configuration.AllTargets;

                        foreach(var target in targets)
                        {
                            if(target is NLog.Targets.FileTarget)
                            {
                                var fTarget = (NLog.Targets.FileTarget)target;
                                var logEventInfo = new NLog.LogEventInfo { TimeStamp = DateTime.Now };
                                var fName = fTarget.FileName.Render(logEventInfo);

                                if(!string.IsNullOrEmpty(fName) && !string.IsNullOrWhiteSpace(fName))
                                {
                                    logDir = Directory.GetParent(fName).FullName;
                                    break;
                                }
                            }
                        }

                        if(string.IsNullOrEmpty(logDir) || string.IsNullOrWhiteSpace(logDir))
                        {
                            // Fallback, just in case.
                            logDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        }

                        // Call process start with the dir path, explorer will handle it.
                        Process.Start(logDir);
                    });
                }

                return m_viewLogsCommand;
            }
        }

        public RelayCommand UseRelaxedPolicyCommand
        {
            get
            {
                if(m_useRelaxedPolicyCommand == null)
                {
                    m_useRelaxedPolicyCommand = new RelayCommand(() =>
                    {
                        m_model.RequestRelaxedPolicy();
                    }, () => AvailableRelaxedRequests > 0);
                }

                return m_useRelaxedPolicyCommand;
            }
        }

        public RelayCommand RelinquishRelaxedPolicyCommand
        {
            get
            {
                if(m_relinquishRelaxedPolicyCommand == null)
                {
                    m_relinquishRelaxedPolicyCommand = new RelayCommand(() =>
                    {
                        m_model.RelinquishRelaxedPolicy();
                    }, () => true);
                }

                return m_relinquishRelaxedPolicyCommand;
            }
        }

        private RelayCommand m_testFilterCommand;
        public RelayCommand TestFilterCommand
        {
            get
            {
                if(m_testFilterCommand == null)
                {
                    m_testFilterCommand = new RelayCommand(() =>
                    {
                        FilterTesting test = new FilterTesting();
                        test.OnFilterTestResult += Test_OnFilterTestResult;
                        DiagnosticsEntries.Clear();
                        testsPassed = 0;
                        testsTotal = 0;

                        Task.Run(() =>
                        {
                            test.TestFilter();
                        });
                    });
                }

                return m_testFilterCommand;
            }
        }

        private RelayCommand m_testSafeSearchCommand;

        public RelayCommand TestSafeSearchCommand
        {
            get
            {
                if(m_testSafeSearchCommand == null)
                {
                    m_testSafeSearchCommand = new RelayCommand(() =>
                    {
                        FilterTesting test = new FilterTesting();
                        test.OnFilterTestResult += Test_OnFilterTestResult;
                        DiagnosticsEntries.Clear();
                        testsPassed = 0;
                        testsTotal = 0;

                        Task.Run(() =>
                        {
                            test.TestDNS();
                        });
                    });
                }

                return m_testSafeSearchCommand;
            }
        }

        private int testsPassed = 0;
        private int testsTotal = 0;

        /// <summary>
        /// Used by filter test to propagate results back to the UI.
        /// </summary>
        /// <param name="test"></param>
        /// <param name="passed"></param>
        private void Test_OnFilterTestResult(DiagnosticsEntry entry)
        {
            // TODO: Build UI for this.
            m_logger.Info("OnFilterTestResult {0} {1}", entry.Test.ToString(), entry.Passed);
            if (entry.Exception != null)
            {
                m_logger.Error("OnFilterTestResult Exception: {0}", entry.Exception.ToString());
            }

            if(entry.Test == FilterTest.BlockingTest)
            {
                CitadelApp.Current.Dispatcher.InvokeAsync(() =>
                {
                    (CitadelApp.Current.MainWindow as Windows.MainWindow).ShowUserMessage("Test Details", entry.Details);
                });
            }

            if(entry.Test == FilterTest.AllTestsCompleted)
            {
                return;
            }

            CitadelApp.Current.Dispatcher.InvokeAsync(() =>
            {
                // FIXME: Don't do CitadelApp.Current.MainWindow as Windows.MainWindow, pass it instead.
                DiagnosticsEntries.Add(new DiagnosticsEntryViewModel(CitadelApp.Current.MainWindow as Windows.MainWindow, entry));
            });
        }

        private ObservableCollection<DiagnosticsEntryViewModel> m_diagnosticsEntries;
        public ObservableCollection<DiagnosticsEntryViewModel> DiagnosticsEntries
        {
            get
            {
                return m_diagnosticsEntries;
            }

            set
            {
                m_diagnosticsEntries = value;
                RaisePropertyChanged(nameof(DiagnosticsEntries));
            }
        }

        private string m_diagnosticsLog;
        public string DiagnosticsLog
        {
            get
            {
                return m_diagnosticsLog;
            }

            set
            {
                m_diagnosticsLog = value;
                RaisePropertyChanged(nameof(DiagnosticsLog));
            }
        }

        public int AvailableRelaxedRequests
        {
            get
            {
                return m_model.AvailableRelaxedRequests;
            }

            set
            {
                m_model.AvailableRelaxedRequests = value;
                RaisePropertyChanged(nameof(AvailableRelaxedRequests));
            }
        }

        public string RelaxedDuration
        {
            get
            {
                return m_model.RelaxedDuration;
            }

            set
            {
                m_model.RelaxedDuration = value;
                RaisePropertyChanged(nameof(RelaxedDuration));
            }
        }

        public string LastSyncStr
        {
            get
            {
                return m_model.LastSyncStr;
            }
        }

        public DateTime LastSync
        {
            get
            {
                return m_model.LastSync;
            }

            set
            {
                m_model.LastSync = value;
                RaisePropertyChanged(nameof(LastSync));
                RaisePropertyChanged(nameof(LastSyncStr));
            }
        }
    }
}