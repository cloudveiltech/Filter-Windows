/*
* Copyright � 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
﻿using CloudVeil.Core.Windows.Client;
using CloudVeil.Core.Windows.IPC;
using CloudVeil.Core.Windows.Util;
using CloudVeil.Core.Windows.Util.Net;
using Filter.Platform.Common;
using Filter.Platform.Common.Client;
using Filter.Platform.Common.IPC;
using Filter.Platform.Common.Net;

namespace CloudVeil.Core.Windows
{
    public static class Platform
    {
        public static void Init()
        {
            FingerprintService.InitFingerprint(new WindowsFingerprint());

            // These loosely typed parameter lists are rather gross. Is there a cleaner way to do this?
            // params: channel, autoReconnect
            PlatformTypes.Register<IPipeClient>((arr) => new WindowsPipeClient((string)arr[0], (arr.Length > 1) ? (bool)arr[1] : false));

            // params: channel
            PlatformTypes.Register<IPipeServer>((arr) => new WindowsPipeServer((string)arr[0]));

            PlatformTypes.Register<IGUIChecks>((arr) => new WindowsGUIChecks());

            PlatformTypes.Register<IAntitampering>((arr) => new WindowsAntitampering());

            PlatformTypes.Register<INetworkInfo>((arr) => new NetworkListUtil());

            PlatformTypes.Register<IAuthenticationStorage>((arr) => new RegistryAuthenticationStorage());

            PlatformTypes.Register<IPathProvider>((arr) => new WindowsPathProvider());

            PlatformTypes.Register<IFilterAgent>((arr) => new WindowsFilterAgent());

            PlatformTypes.Register<IFilterUpdater>((arr) => new WindowsFilterUpdater());
        }
    }
}
