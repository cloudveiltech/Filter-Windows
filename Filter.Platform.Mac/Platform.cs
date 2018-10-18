// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using Filter.Platform.Common;
using Filter.Platform.Common.Client;
using Filter.Platform.Common.IPC;
using Filter.Platform.Common.Net;
using Filter.Platform.Common.Util;

namespace Filter.Platform.Mac
{
    public static class Platform
    {
        public const string NativeLib = "Filter.Platform.Mac.Native";

        public static void Init()
        {
            FingerprintService.InitFingerprint(new MacFingerprint());

            PlatformTypes.Register<IPipeServer>((arr) => new MacPipeServer());

            // params: channel
            PlatformTypes.Register<IPipeClient>((arr) => new MacPipeClient((string)arr[0]));

            PlatformTypes.Register<IGUIChecks>((arr) => new MacGUIChecks());

            PlatformTypes.Register<IAntitampering>((arr) => new MacAntitampering());

            PlatformTypes.Register<INetworkInfo>((arr) => new MacNetworkInfo());

            PlatformTypes.Register<IAuthenticationStorage>((arr) => new FileAuthenticationStorage());

            PlatformTypes.Register<IPathProvider>((arr) => new MacPathProvider());

            // These loosely typed parameter lists are rather gross. Is there a cleaner way to do this?
            // params: channel, autoReconnect
            /*

            PlatformTypes.Register<IPathProvider>((arr) => new WindowsPathProvider());*/
        }
    }
}
