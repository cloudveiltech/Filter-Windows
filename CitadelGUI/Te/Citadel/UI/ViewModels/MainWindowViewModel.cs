/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using GalaSoft.MvvmLight.Command;
using System;
using Te.Citadel.UI.Models;

namespace Te.Citadel.UI.ViewModels
{
    public class MainWindowViewModel : BaseCitadelViewModel
    {
        private MainWindowModel m_model;

        public bool InternetIsConnected
        {
            get
            {
                return m_model.InternetIsConnected;
            }
        }

        private bool m_showGuestNetwork;

        /// <summary>
        /// Bound to IsOpen on the guest network flyout.
        /// </summary>
        public bool ShowIsGuestNetwork
        {
            get
            {
                return m_showGuestNetwork;
            }

            set
            {
                m_showGuestNetwork = value;
                RaisePropertyChanged(nameof(ShowIsGuestNetwork));
            }
        }

        private bool m_isCaptivePortalActive;

        /// <summary>
        /// If this is true, we show guest network window command.
        /// </summary>
        public bool IsCaptivePortalActive
        {
            get
            {
                return m_isCaptivePortalActive;
            }

            set
            {
                m_isCaptivePortalActive = value;
                RaisePropertyChanged(nameof(IsCaptivePortalActive));
            }
        }

        private RelayCommand m_openGuestNetwork;
        
        public RelayCommand OpenGuestNetwork
        {
            get
            {
                if(m_openGuestNetwork == null)
                {
                    m_openGuestNetwork = new RelayCommand((Action)(() =>
                    {
                        ShowIsGuestNetwork = true;
                    }));
                }

                return m_openGuestNetwork;
            }
        }

        public MainWindowViewModel()
        {
            m_model = new MainWindowModel();
            m_model.PropertyChanged += OnModelChange;
        }

        private void OnModelChange(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch(e.PropertyName)
            {
                case nameof(InternetIsConnected):
                    {
                        RaisePropertyChanged(nameof(InternetIsConnected));
                    }
                    break;
            }
        }
    }
}