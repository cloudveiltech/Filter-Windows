/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Util;
using Citadel.Core.Windows.Util.Net;
using Filter.Platform.Common.Net;
using GalaSoft.MvvmLight;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using Te.Citadel.Util;

namespace Te.Citadel.UI.Models
{
    internal class MainWindowModel : ObservableObject
    {
        private volatile bool m_internetIsConnected = false;
        
        public MainWindowModel()
        {
            InitInetMonitoring();
        }

        private void InitInetMonitoring()
        {
            this.InternetIsConnected = NetworkStatus.Default.HasIpv4InetConnection || NetworkStatus.Default.HasIpv6InetConnection;

            NetworkStatus.Default.ConnectionStateChanged += () =>
            {
                this.InternetIsConnected = NetworkStatus.Default.HasIpv4InetConnection || NetworkStatus.Default.HasIpv6InetConnection;
            };
        }

        public bool InternetIsConnected
        {
            get
            {
                return m_internetIsConnected;
            }

            private set
            {
                m_internetIsConnected = value;
                RaisePropertyChanged(nameof(InternetIsConnected));
            }
        }
    }
}