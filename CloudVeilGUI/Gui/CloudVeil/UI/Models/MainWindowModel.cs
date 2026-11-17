/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Filter.Platform.Common.Net;
using GalaSoft.MvvmLight;

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
            InternetIsConnected = NetworkStatus.Default.HasConnection;

            NetworkStatus.Default.ConnectionStateChanged += () =>
            {
                InternetIsConnected = NetworkStatus.Default.HasConnection;
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
