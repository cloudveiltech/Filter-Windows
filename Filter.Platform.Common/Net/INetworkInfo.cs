/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common.Net
{
    public interface INetworkInfo
    {
        bool BehindIPv4CaptivePortal { get; }

        bool BehindIPv6CaptivePortal { get; }

        bool BehindIPv4Proxy { get; }

        bool BehindIPv6Proxy { get; }

        bool HasIPv4InetConnection { get; }

        bool HasIPv6InetConnection { get; }

        event ConnectionStateChangeHandler ConnectionStateChanged;
    }
}
