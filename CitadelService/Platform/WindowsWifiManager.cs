/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Filter.Platform.Common.Util;
using FilterProvider.Common.Platform;
using NativeWifi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitadelService.Platform
{
    public class WindowsWifiManager : IWifiManager
    {
        private static WlanClient s_wlanClient = null;

        static WindowsWifiManager()
        {
            s_wlanClient = new WlanClient();
        }

        public List<string> DetectCurrentSsids()
        {
            try
            {
                List<string> connectedSsids = new List<string>();

                foreach (WlanClient.WlanInterface wlanInterface in s_wlanClient.Interfaces)
                {
                    Wlan.Dot11Ssid ssid = wlanInterface.CurrentConnection.wlanAssociationAttributes.dot11Ssid;
                    connectedSsids.Add(new string(Encoding.ASCII.GetChars(ssid.SSID, 0, (int)ssid.SSIDLength)));
                }

                return connectedSsids;
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
                return null;
            }
        }
    }
}
