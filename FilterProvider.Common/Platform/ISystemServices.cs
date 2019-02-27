/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using FilterProvider.Common.Data;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace FilterProvider.Common.Platform
{
    /// <summary>
    /// 
    /// </summary>
    public interface ISystemServices
    {
        /// <summary>
        /// This event gets called by the system when the current user is being logged off or the computer is getting shut down.
        /// </summary>
        event EventHandler SessionEnding;

        /// <summary>
        /// Call this to ensure that our process has access to the outside world through any firewalls present on the system.
        /// </summary>
        void EnsureFirewallAccess();

        /// <summary>
        /// Platform-specific implementation of anti-tampering protection
        /// </summary>
        void RunProtectiveServices();

        /// <summary>
        /// Returns a platform-specific proxy server implementation.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        IProxyServer StartProxyServer(ProxyConfiguration config);

        /// <summary>
        /// Use this to enable internet after we've disabled it.
        /// </summary>
        void EnableInternet();

        /// <summary>
        /// Platform-specific way to disable internet. Used in such features as cooldown.
        /// </summary>
        void DisableInternet();

        /// <summary>
        /// Does not need to be available until after OnStartProxy is fired.
        /// </summary>
        X509Certificate2 RootCertificate { get; }

        /// <summary>
        /// Use this to determine when the proxy actually starts. Do not depend on root certificate being non-null until this event has fired once.
        /// </summary>
        event EventHandler OnStartProxy;

        // TODO: Rename IAntitampering stuff to something different.
    }
}
