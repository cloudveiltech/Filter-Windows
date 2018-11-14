/*
* Copyright © 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Te.Citadel.UI.Models.DashboardModel;

namespace Te.Citadel.UI.ViewModels
{
    public class SettingsViewModel : BaseCitadelViewModel
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

        private RelayCommand m_useRelaxedPolicyCommand;
        public RelayCommand UseRelaxedPolicyCommand
        {
            get
            {
                if (m_useRelaxedPolicyCommand == null)
                {
                    m_useRelaxedPolicyCommand = new RelayCommand(() =>
                    {
                        this.RequestRelaxedPolicy();
                    }, () => AvailableRelaxedRequests > 0);
                }

                return m_useRelaxedPolicyCommand;
            }
        }

        private RelayCommand m_relinquishRelaxedPolicyCommand;
        public RelayCommand RelinquishRelaxedPolicyCommand
        {
            get
            {
                if (m_relinquishRelaxedPolicyCommand == null)
                {
                    m_relinquishRelaxedPolicyCommand = new RelayCommand(() =>
                    {
                        this.RelinquishRelaxedPolicy();
                    }, () => true);
                }

                return m_relinquishRelaxedPolicyCommand;
            }
        }
    }
}
