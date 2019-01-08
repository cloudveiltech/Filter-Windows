/*
* Copyright © 2019 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using Citadel.IPC;
using Filter.Platform.Common.Types;
using Filter.Platform.Common.Util;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.UI.Views;

namespace Te.Citadel.UI.ViewModels
{
    public class AdvancedViewModel : BaseCitadelViewModel
    {

        /// <summary>
        /// Private data member for the public DeactivateCommand property.
        /// </summary>
        private RelayCommand m_deactivationCommand;

        /// <summary>
        /// Private data member for the public ViewLogsCommand property.
        /// </summary>
        private RelayCommand m_viewLogsCommand;

        private RelayCommand m_viewSslExemptionsCommand;

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
                if (m_viewLogsCommand == null)
                {
                    m_viewLogsCommand = new RelayCommand(() =>
                    {
                        // Scan all Nlog log targets
                        var logDir = string.Empty;

                        var targets = NLog.LogManager.Configuration.AllTargets;

                        foreach (var target in targets)
                        {
                            if (target is NLog.Targets.FileTarget)
                            {
                                var fTarget = (NLog.Targets.FileTarget)target;
                                var logEventInfo = new NLog.LogEventInfo { TimeStamp = DateTime.Now };
                                var fName = fTarget.FileName.Render(logEventInfo);

                                if (!string.IsNullOrEmpty(fName) && !string.IsNullOrWhiteSpace(fName))
                                {
                                    logDir = Directory.GetParent(fName).FullName;
                                    break;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(logDir) || string.IsNullOrWhiteSpace(logDir))
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

        public RelayCommand ViewSslExemptionsCommand
        {
            get
            {
                if (m_viewSslExemptionsCommand == null)
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
                if (m_deactivationCommand == null)
                {
                    m_deactivationCommand = new RelayCommand((Action)(() =>
                    {
                        try
                        {
                            Task.Run(() =>
                            {
                                using (var ipcClient = new IPCClient())
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
                        catch (Exception e)
                        {
                            LoggerUtil.RecursivelyLogException(m_logger, e);
                        }
                    }));
                }

                return m_deactivationCommand;
            }
        }
    }
}
