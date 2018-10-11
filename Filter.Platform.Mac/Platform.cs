// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using Filter.Platform.Common;

namespace Filter.Platform.Mac
{
    public static class Platform
    {
        public static void Init()
        {
            FingerprintService.InitFingerprint(new MacFingerprint());

            // These loosely typed parameter lists are rather gross. Is there a cleaner way to do this?
            // params: channel, autoReconnect
            /*PlatformTypes.Register<IPipeClient>((arr) => new WindowsPipeClient((string)arr[0], (arr.Length > 1) ? (bool)arr[1] : false));

            // params: channel
            PlatformTypes.Register<IPipeServer>((arr) => new WindowsPipeServer((string)arr[0]));

            PlatformTypes.Register<IGUIChecks>((arr) => new WindowsGUIChecks());

            PlatformTypes.Register<IAntitampering>((arr) => new WindowsAntitampering());

            PlatformTypes.Register<INetworkInfo>((arr) => new NetworkListUtil());

            PlatformTypes.Register<IAuthenticationStorage>((arr) => new RegistryAuthenticationStorage());

            PlatformTypes.Register<IPathProvider>((arr) => new WindowsPathProvider());*/
        }
    }
}
