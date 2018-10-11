// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.Net;
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

        public void SetDnsForNic(string nicName, IPAddress primary, IPAddress secondary)
        {
            throw new NotImplementedException();
        }

        public void SetDnsForNicToDHCP(string nicName)
        {
            throw new NotImplementedException();
        }
    }
}
