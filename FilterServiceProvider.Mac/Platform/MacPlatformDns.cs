// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.Net;
using System.Runtime.InteropServices;
using Filter.Platform.Common.Util;
using FilterProvider.Common.Platform;

namespace FilterServiceProvider.Mac.Platform
{
    public class MacPlatformDns : IPlatformDns
    {
        public MacPlatformDns()
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        private NLog.Logger logger;

        [DllImport("Filter.Platform.Mac.Native")]
        private static extern bool EnforceDns(string primaryDns, string secondaryDns);

        public void SetDnsForNic(string nicName, IPAddress primary, IPAddress secondary)
        {
            throw new NotImplementedException();
        }

        public void SetDnsForNicToDHCP(string nicName)
        {
            throw new NotImplementedException();
        }

        public bool SetDnsForAllInterfaces(IPAddress primary, IPAddress secondary)
        {
            return EnforceDns(primary?.ToString(), secondary?.ToString());
        }

        public bool SetDnsForAllInterfacesToDHCP()
        {
            return EnforceDns(null, null);
        }
    }
}
