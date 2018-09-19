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

namespace CitadelService.Platform
{
    public class WindowsDns : IPlatformDns
    {
        private NLog.Logger m_logger;
        public WindowsDns()
        {
            m_logger = LoggerUtil.GetAppWideLogger();
        }

        public void SetDnsForNic(string nicName, IPAddress primary, IPAddress secondary)
        {
            using (var networkConfigMng = new ManagementClass("Win32_NetworkAdapterConfiguration"))
            {
                using (var networkConfigs = networkConfigMng.GetInstances())
                {
                    foreach (var managementObject in networkConfigs.Cast<ManagementObject>().Where(objMO => (bool)objMO["IPEnabled"] && objMO["Description"].Equals(nicName)))
                    {
                        using (var newDNS = managementObject.GetMethodParameters("SetDNSServerSearchOrder"))
                        {
                            List<string> dnsServers = new List<string>();
                            var existingDns = (string[])newDNS["DNSServerSearchOrder"];
                            if (existingDns != null && existingDns.Length > 0)
                            {
                                dnsServers = new List<string>(existingDns);
                            }

                            bool changed = false;

                            if (primary != null)
                            {
                                if (!dnsServers.Contains(primary.ToString()))
                                {
                                    dnsServers.Insert(0, primary.ToString());
                                    changed = true;
                                }
                            }
                            if (secondary != null)
                            {
                                if (!dnsServers.Contains(secondary.ToString()))
                                {
                                    changed = true;

                                    if (dnsServers.Count > 0)
                                    {
                                        dnsServers.Insert(1, secondary.ToString());
                                    }
                                    else
                                    {
                                        dnsServers.Add(secondary.ToString());
                                    }
                                }
                            }

                            if (changed)
                            {
                                newDNS["DNSServerSearchOrder"] = dnsServers.ToArray();
                                managementObject.InvokeMethod("SetDNSServerSearchOrder", newDNS, null);
                            }
                        }
                    }
                }
            }
        }

        public void SetDnsForNicToDHCP(string nicName)
        {
            using (var networkConfigMng = new ManagementClass("Win32_NetworkAdapterConfiguration"))
            {
                using (var networkConfigs = networkConfigMng.GetInstances())
                {
                    foreach (var managementObject in networkConfigs.Cast<ManagementObject>().Where(objMO => (bool)objMO["IPEnabled"] && objMO["Description"].Equals(nicName)))
                    {
                        ManagementBaseObject inParams = managementObject.GetMethodParameters("SetDNSServerSearchOrder");
                        inParams["DNSServerSearchOrder"] = new string[0];
                        ManagementBaseObject result = managementObject.InvokeMethod("SetDNSServerSearchOrder", inParams, null);
                        UInt32 ret = (UInt32)result.Properties["ReturnValue"].Value;

                        if (ret != 0)
                        {
                            m_logger.Warn("Unable to change DNS Server settings back to DHCP. Error code {0} https://msdn.microsoft.com/en-us/library/aa393295(v=vs.85).aspx for more info.", ret);
                        }
                        else
                        {
                            m_logger.Info("Changed adapter {0} to DHCP.", managementObject["Description"]);
                        }
                    }
                }
            }
        }
    }
}
