/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Filter.Platform.Common.Util;
using FilterProvider.Common.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Diagnostics;
using WindowsFirewallHelper;

namespace CloudVeilService.Platform
{
    public class WindowsDns : IPlatformDns
    {
        private NLog.Logger logger;
        public WindowsDns()
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        public void SetDnsForNic(string nicName, IPAddress primary, IPAddress secondary)
        {
            var ipVersion = "ipv4";
            
            if(primary != null && primary.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                ipVersion = "ipv6";
            } 
            else if (secondary != null && secondary.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                ipVersion = "ipv6";
            }
              
            var command = $"interface {ipVersion} add dnsservers \"{nicName}\" address=";
            if (primary != null)
            {
                var process = new Process();
                process.StartInfo = new ProcessStartInfo("netsh", command + primary.ToString() + " index=1");
                process.Start();
                process.WaitForExit();
            }

            if (secondary != null)
            {
                var process = new Process();
                process.StartInfo = new ProcessStartInfo("netsh", command + secondary.ToString() + " index=2");
                process.Start();
                process.WaitForExit();
            }
            logger.Info("Changed DNS settings for NIC {0}", nicName);
        }

        public void SetDnsForNicToDHCP(string nicName)
        {
            var command = $"interface ipv4 set dnsservers \"{nicName}\" source=dhcp";
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo("netsh", command);
            process.Start();
            process.WaitForExit();

            command = $"interface ipv6 set dnsservers \"{nicName}\" source=dhcp";
            process = new Process();
            process.StartInfo = new ProcessStartInfo("netsh", command);
            process.Start();
            process.WaitForExit();
        }

        public bool SetDnsForAllInterfaces(IPAddress primaryDns, IPAddress secondaryDns)
        {
            var ifaces = NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Tunnel);
            bool ranUpdate = false;

            foreach (var iface in ifaces)
            {
                bool needsUpdate = false;

                if (primaryDns != null && !iface.GetIPProperties().DnsAddresses.Contains(primaryDns))
                {
                    needsUpdate = true;
                }
                if (secondaryDns != null && !iface.GetIPProperties().DnsAddresses.Contains(secondaryDns))
                {
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    ranUpdate = true;
                    SetDnsForNic(iface.Name, primaryDns, secondaryDns);
                }
            }

            return ranUpdate;
        }

        public bool SetDnsForAllInterfacesToDHCP()
        {
            var ifaces = NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up && x.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

            foreach (var iface in ifaces)
            {
                SetDnsForNicToDHCP(iface.Name);
            }

            return true;
        }
    }
}
