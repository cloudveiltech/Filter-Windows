/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Filter.Platform.Common.Util;
using Citadel.IPC;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Filter.Platform.Common.Types;
using CloudVeilGUI.Common;
using System.ComponentModel;

namespace CloudVeilGUI.ViewModels
{
    public class AdvancedPageViewModel : INotifyPropertyChanged
    {
        public AdvancedPageViewModel()
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        private NLog.Logger logger;

        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private bool syncRequestInProgress;
        public bool SyncRequestInProgress
        {
            get => syncRequestInProgress;
            set
            {
                syncRequestInProgress = value;
                RaisePropertyChanged(nameof(SyncRequestInProgress));
            }
        }

        private bool settingsUpToDate;
        public bool SettingsUpToDate
        {
            get => settingsUpToDate;
            set
            {
                settingsUpToDate = value;
                RaisePropertyChanged(nameof(SettingsUpToDate));
            }
        }

        private bool isSyncButtonEnabled;
        public bool IsSyncButtonEnabled
        {
            get => isSyncButtonEnabled;
            set
            {
                isSyncButtonEnabled = value;
                RaisePropertyChanged(nameof(IsSyncButtonEnabled));
            }
        }

        private string syncSettingsText;
        public string SyncSettingsText
        {
            get => syncSettingsText;
            set
            {
                syncSettingsText = value;
                RaisePropertyChanged(nameof(SyncSettingsText));
            }
        }

        private bool syncErrorOccurred;
        public bool SyncErrorOccurred
        {
            get => syncErrorOccurred;
            set
            {
                syncErrorOccurred = value;
                RaisePropertyChanged(nameof(SyncErrorOccurred));
            }
        }

        private string syncErrorText;
        public string SyncErrorText
        {
            get => syncErrorText;
            set
            {
                syncErrorText = value;
                RaisePropertyChanged(nameof(SyncErrorText));
            }
        }

        private bool newVersionAvailable;
        public bool NewVersionAvailable
        {
            get => newVersionAvailable;
            set
            {
                newVersionAvailable = value;
                RaisePropertyChanged(nameof(NewVersionAvailable));
            }
        }

        private Command deactivateCommand;
        public Command DeactivateCommand
        {
            get
            {
                if(deactivateCommand == null)
                {
                    deactivateCommand = new Command(() =>
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
                        catch(Exception ex)
                        {
                            LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), ex);
                        }
                    });
                }

                return deactivateCommand;
            }
        }

        private Command checkForUpdatesCommand;
        public Command CheckForUpdatesCommand
        {
            get
            {
                if(checkForUpdatesCommand == null)
                {
                    checkForUpdatesCommand = new Command(() =>
                    {
                        Task.Run(() =>
                        {
                            using (IPCClient client = new IPCClient(false))
                            {
                                client.ConnectedToServer = () =>
                                {
                                    client.RequestConfigUpdate((message) =>
                                    {
                                        if (message.UpdateResult.HasFlag(ConfigUpdateResult.AppUpdateAvailable))
                                        {
                                            NewVersionAvailable = true;
                                        }
                                    });
                                };
                            }
                        });
                    });
                }

                return checkForUpdatesCommand;
            }
        }

        private Command synchronizeSettingsCommand;
        public Command SynchronizeSettingsCommand
        {
            get
            {
                if(synchronizeSettingsCommand == null)
                {
                    synchronizeSettingsCommand = new Command(() =>
                    {
                        Task.Run(() =>
                        {
                            using (IPCClient client = new IPCClient(false))
                            {
                                client.ConnectedToServer = () =>
                                {
                                    client.RequestConfigUpdate((message) =>
                                    {
                                        logger.Info("We got a config update message back.");
                                        SyncRequestInProgress = false;

                                        
                                        switch (message.UpdateResult)
                                        {
                                            case ConfigUpdateResult.UpToDate:
                                                SettingsUpToDate = true;
                                                IsSyncButtonEnabled = false;
                                                SyncSettingsText = "Up to date";
                                                break;

                                            case ConfigUpdateResult.Updated:
                                                SettingsUpToDate = true;
                                                IsSyncButtonEnabled = false;
                                                SyncSettingsText = "Updated";
                                                break;

                                            case ConfigUpdateResult.NoInternet:
                                                IsSyncButtonEnabled = true;
                                                SettingsUpToDate = false;
                                                SyncErrorOccurred = true;
                                                SyncSettingsText = "Try Again";
                                                SyncErrorText = "No internet";
                                                break;

                                            case ConfigUpdateResult.ErrorOccurred:
                                                IsSyncButtonEnabled = true;
                                                SettingsUpToDate = false;
                                                SyncErrorOccurred = true;
                                                SyncSettingsText = "Try Again";
                                                SyncErrorText = "Error occurred";
                                                break;

                                            default:
                                                SettingsUpToDate = false;
                                                SyncErrorOccurred = true;
                                                SyncSettingsText = "Try Again";
                                                SyncErrorText = "Unrecognized";
                                                break;
                                        }

                                        var timer = new System.Timers.Timer(30000);
                                        timer.Elapsed += (sender, e) =>
                                        {
                                            IsSyncButtonEnabled = true;
                                            SettingsUpToDate = false;
                                            SyncErrorOccurred = false;
                                            SyncSettingsText = "Sync";
                                            SyncErrorText = "";

                                            timer.Dispose();
                                        };
                                        timer.Enabled = true;

                                        //}


                                        // TODO: Add code to display on dashboard view model.
                                    });
                                };

                                client.WaitForConnection();
                                Task.Delay(3000).Wait(); // FIXME Surely there's a good way to detect when our work is over with IPCClients
                            }
                        });

                    });
                }

                return synchronizeSettingsCommand;
            }
        }

        private Command viewCertErrorsCommand;
        public Command ViewCertErrorsCommand
        {
            get
            {
                if(viewCertErrorsCommand == null)
                {
                    viewCertErrorsCommand = new Command(() =>
                    {
                        //CommonAppServices.Default.GuiServices.ShowCertificateErrorsScreen();
                    });
                }

                return viewCertErrorsCommand;
            }
        }
    }
}
