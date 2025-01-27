/*
* Copyright © 2019 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;
using static Gui.CloudVeil.UI.Models.DashboardModel;

namespace Gui.CloudVeil.UI.ViewModels
{
    public class RelaxedPolicyViewModel : BaseCloudVeilViewModel
    {
        public event RelaxedPolicyRequestDelegate RelaxedPolicyRequested;

        public event RelaxedPolicyRequestDelegate RelinquishRelaxedPolicyRequested;

        public void RequestRelaxedPolicy()
        {
            RelaxedPolicyRequested?.Invoke();
        }

        public void RelinquishRelaxedPolicy()
        {
            RelinquishRelaxedPolicyRequested?.Invoke();
        }

        private bool isRelaxedPolicyInEffect;
        public bool IsRelaxedPolicyInEffect
        {
            get => isRelaxedPolicyInEffect;
            set
            {
                isRelaxedPolicyInEffect = value;
                RaisePropertyChanged(nameof(IsRelaxedPolicyInEffect));
            }
        }

        private int availableRelaxedRequests;
        public int AvailableRelaxedRequests
        {
            get
            {
                return availableRelaxedRequests;
            }

            set
            {
                availableRelaxedRequests = value;
                RaisePropertyChanged(nameof(AvailableRelaxedRequests));
            }
        }

        private string relaxedDuration;
        public string RelaxedDuration
        {
            get
            {
                return relaxedDuration;
            }

            set
            {
                relaxedDuration = value;
                RaisePropertyChanged(nameof(RelaxedDuration));
            }
        }

        public string LastSyncStr
        {
            get
            {
                return string.Format("Last Updated: {0:f}", lastSync);
            }
        }

        private DateTime lastSync;
        public DateTime LastSync
        {
            get
            {
                return lastSync;
            }

            set
            {
                lastSync = value;
                RaisePropertyChanged(nameof(LastSync));
                RaisePropertyChanged(nameof(LastSyncStr));
            }
        }

        public string RelaxedPolicySetupUri
            => global::CloudVeil.CompileSecrets.ServiceProviderUserRelaxedPolicyPath;

        private RelayCommand useRelaxedPolicyCommand;
        public RelayCommand UseRelaxedPolicyCommand
        {
            get
            {
                if (useRelaxedPolicyCommand == null)
                {
                    useRelaxedPolicyCommand = new RelayCommand(() =>
                    {
                        this.RequestRelaxedPolicy();
                    }, () => AvailableRelaxedRequests > 0);
                }

                return useRelaxedPolicyCommand;
            }
        }

        private RelayCommand relinquishRelaxedPolicyCommand;
        public RelayCommand RelinquishRelaxedPolicyCommand
        {
            get
            {
                if (relinquishRelaxedPolicyCommand == null)
                {
                    relinquishRelaxedPolicyCommand = new RelayCommand(() =>
                    {
                        this.RelinquishRelaxedPolicy();
                    }, () => true);
                }

                return relinquishRelaxedPolicyCommand;
            }
        }
    }
}
