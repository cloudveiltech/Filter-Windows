// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using Filter.Platform.Common;
using FilterProvider.Common.Platform;
using FilterServiceProvider.Mac.Platform;

namespace FilterServiceProvider.Mac.Services
{
    public class FilterServiceProvider
    {
        public FilterServiceProvider()
        {
            PlatformTypes.Register<IPlatformDns>((arr) => new MacPlatformDns());
            PlatformTypes.Register<IWifiManager>((arr) => new MacWifiManager());
            PlatformTypes.Register<IPlatformTrust>((arr) => new MacTrustManager());
            PlatformTypes.Register<ISystemServices>((arr) => new MacSystemServices());
        }
    }
}
