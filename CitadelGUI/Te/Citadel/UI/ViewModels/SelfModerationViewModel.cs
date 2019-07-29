/*
* Copyright © 2019 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using Citadel.IPC;
using CloudVeil.Windows;
using CloudVeilGUI;
using GalaSoft.MvvmLight.CommandWpf;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.UI.Windows;

namespace Te.Citadel.UI.ViewModels
{
    public class SelfModerationViewModel : BaseCitadelViewModel
    {
        public string SelfModerationSetupUri
            => CloudVeil.CompileSecrets.ServiceProviderUserSelfModerationPath.Replace("{{identifier}}", ActivationIdentifier);

        private string activationIdentifier;
        public string ActivationIdentifier
        {
            get => activationIdentifier;
            set
            {
                activationIdentifier = value;
                RaisePropertyChanged(nameof(ActivationIdentifier));
                RaisePropertyChanged(nameof(SelfModerationSetupUri));
            }
        }

        private string newSelfModerationSite;
        public string NewSelfModerationSite
        {
            get
            {
                return newSelfModerationSite;
            }

            set
            {
                newSelfModerationSite = value;
                RaisePropertyChanged(nameof(NewSelfModerationSite));
            }
        }

        private string newTrigger;
        public string NewTrigger
        {
            get => newTrigger;

            set
            {
                newTrigger = value;
                RaisePropertyChanged(nameof(NewTrigger));
            }
        }

        private ObservableCollection<string> selfModerationSites = new ObservableCollection<string>();
        public ObservableCollection<string> SelfModerationSites
        {
            get
            {
                return selfModerationSites;
            }

            set
            {
                selfModerationSites = value;
                RaisePropertyChanged(nameof(SelfModerationSites));
            }
        }

        private ObservableCollection<string> triggerBlacklist = new ObservableCollection<string>();
        public ObservableCollection<string> TriggerBlacklist
        {
            get => triggerBlacklist;
            set
            {
                triggerBlacklist = value;
                RaisePropertyChanged(nameof(TriggerBlacklist));
            }
        }

        private RelayCommand<string> addNewSiteCommand;
        public RelayCommand<string> AddNewSiteCommand
        {
            get
            {
                if(addNewSiteCommand == null)
                {
                    addNewSiteCommand = new RelayCommand<string>(async (site) =>
                    {
                        var window = (CitadelApp.Current.MainWindow as BaseWindow);

                        bool result = await (CitadelApp.Current.MainWindow as BaseWindow).AskUserYesNoQuestion("Are you sure?", $"This will add '{site}' to your list of blocked sites. Are you sure you want to continue?");

                        if (!result)
                            return;

                        IPCClient.Default.RequestAddSelfModeratedSite(site)
                            .OnReply((context, msg) =>
                            {
                                CitadelApp.Current.Dispatcher.Invoke(() =>
                                {
                                    SelfModerationSites = new ObservableCollection<string>(msg.DataObject as List<string>);
                                });

                                return true;
                            });
                    });
                }

                return addNewSiteCommand;
            }
        }
    }
}
