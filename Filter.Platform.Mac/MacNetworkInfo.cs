// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using Filter.Platform.Common.Net;

namespace Filter.Platform.Mac
{
    public class MacNetworkInfo : INetworkInfo
    {
        public MacNetworkInfo()
        {
        }

        public bool BehindIPv4CaptivePortal => throw new NotImplementedException();

        public bool BehindIPv6CaptivePortal => throw new NotImplementedException();

        public bool BehindIPv4Proxy => throw new NotImplementedException();

        public bool BehindIPv6Proxy => throw new NotImplementedException();

        public bool HasIPv4InetConnection => throw new NotImplementedException();

        public bool HasIPv6InetConnection => throw new NotImplementedException();

        public event ConnectionStateChangeHandler ConnectionStateChanged;
    }
}
