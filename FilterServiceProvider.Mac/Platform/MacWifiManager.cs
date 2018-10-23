// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.Collections.Generic;
using FilterProvider.Common.Platform;
using CoreWlan;

namespace FilterServiceProvider.Mac.Platform
{
    public class MacWifiManager : IWifiManager
    {
        public MacWifiManager()
        {
        }

        public List<string> DetectCurrentSsids()
        {
            List<string> currentConnected = new List<string>();

            CWWiFiClient client = CWWiFiClient.SharedWiFiClient;

            CWInterface[] wifiInterfaces = client.Interfaces;

            foreach(var iface in wifiInterfaces)
            {
                string ssid = iface.Ssid;

                if (ssid != null)
                {
                    currentConnected.Add(ssid);
                }
            }

            return currentConnected;
        }
    }
}
