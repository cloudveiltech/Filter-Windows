﻿/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeil.Core.Windows.Util;
using CloudVeil.Core.Windows.Util.Net;
using Filter.Platform.Common.Net;
using GalaSoft.MvvmLight;
using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using Gui.CloudVeil.Util;

namespace Gui.CloudVeil.UI.Models
{
    internal class MainWindowModel : ObservableObject
    {
        private volatile bool internetIsConnected = false;
        
        public MainWindowModel()
        {
            InitInetMonitoring();
        }

        private void InitInetMonitoring()
        {
            this.InternetIsConnected = NetworkStatus.Default.HasConnection;

            NetworkStatus.Default.ConnectionStateChanged += () =>
            {
                this.InternetIsConnected = NetworkStatus.Default.HasConnection;
            };
        }

        public bool InternetIsConnected
        {
            get
            {
                return internetIsConnected;
            }

            private set
            {
                internetIsConnected = value;
                RaisePropertyChanged(nameof(InternetIsConnected));
            }
        }
    }
}