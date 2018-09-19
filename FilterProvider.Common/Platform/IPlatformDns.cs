/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace FilterProvider.Common.Platform
{
    public interface IPlatformDns
    {
        /// <summary>
        /// Sets the specified NIC's DNS settings to the two specified IP addresses.
        /// </summary>
        /// <param name="nicName">The name of the network interface to set the IP addresses on.</param>
        /// <param name="primary">The first DNS server to use.</param>
        /// <param name="secondary">The second DNS server to use.</param>
        void SetDnsForNic(string nicName, IPAddress primary, IPAddress secondary);

        void SetDnsForNicToDHCP(string nicName);
    }
}
