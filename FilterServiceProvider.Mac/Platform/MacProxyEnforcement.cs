// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.Runtime.InteropServices;

namespace FilterServiceProvider.Mac.Platform
{
    public static class MacProxyEnforcement
    {
        [DllImport(Filter.Platform.Mac.Platform.NativeLib)]
        public static extern bool SetProxy(string hostname, int httpPort, int httpsPort);
    }
}
