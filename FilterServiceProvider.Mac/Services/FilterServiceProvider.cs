// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using Filter.Platform.Common;
using FilterProvider.Common.Platform;
using FilterProvider.Common.Services;
using FilterServiceProvider.Mac.Platform;

namespace FilterServiceProvider.Mac.Services
{
    public class FilterServiceProvider
    {
        private CommonFilterServiceProvider commonProvider;

        public FilterServiceProvider()
        {
            Filter.Platform.Mac.Platform.Init();

            PlatformTypes.Register<IPlatformDns>((arr) => new MacPlatformDns());
            PlatformTypes.Register<IWifiManager>((arr) => new MacWifiManager());
            PlatformTypes.Register<IPlatformTrust>((arr) => new MacTrustManager());
            PlatformTypes.Register<ISystemServices>((arr) => new MacSystemServices());

            commonProvider = new CommonFilterServiceProvider();
        }

        public bool Start()
        {
            System.AppDomain.CurrentDomain.FirstChanceException += (object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e) =>
            {
                Console.WriteLine("First Chance exception: {0}", e.Exception);
            };

            commonProvider.OnStopFiltering += (sender, e) =>
            {
                MacProxyEnforcement.SetProxy(null, 0, 0);
            };

            Console.WriteLine("Starting common filter provider.");
            return commonProvider.Start();
        }

        public bool Stop()
        {
            return commonProvider.Stop();
        }

        public bool Shutdown()
        {
            return commonProvider.Shutdown();
        }
    }
}
