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
    /// <summary>
    /// Handler for network state change notifications.
    /// </summary>
    public delegate void ConnectionStateChangeHandler();

    /// <summary>
    /// Class we use to analayze network state information. This is a bit repetative but abstracts
    /// away our underlying implementation.
    /// </summary>
    public class NetworkStatus
    {
        public event ConnectionStateChangeHandler ConnectionStateChanged;

        private static NetworkStatus s_instance;

        static NetworkStatus()
        {
            s_instance = new NetworkStatus();
        }

        public static NetworkStatus Default
        {
            get
            {
                return s_instance;
            }
        }

        private INetworkInfo m_nListUtil;

        /// <summary>
        /// Gets whether or not the device has internet access that is not proxied nor behind a
        /// captive portal.
        /// </summary>
        public bool HasUnencumberedInternetAccess
        {
            get
            {
                return (HasIpv4InetConnection || HasIpv6InetConnection) && (!BehindIPv4CaptivePortal && !BehindIPv6CaptivePortal) && (!BehindIPv4Proxy && !BehindIPv6Proxy);
            }
        }

        /// <summary>
        /// Gets whether or not any of the device IPV4 connections have detected that they are behind a
        /// captive portal.
        /// </summary>
        public bool BehindIPv4CaptivePortal
        {
            get
            {
                return m_nListUtil.BehindIPv4CaptivePortal;
            }
        }

        /// <summary>
        /// Gets whether or not any of the device IPV6 connections have detected that they are behind a
        /// captive portal.
        /// </summary>
        public bool BehindIPv6CaptivePortal
        {
            get
            {
                return m_nListUtil.BehindIPv6CaptivePortal;
            }
        }

        /// <summary>
        /// Gets whether or not any of the device IPV4 connections have detected that they are behind a
        /// proxy.
        /// </summary>
        public bool BehindIPv4Proxy
        {
            get
            {
                return m_nListUtil.BehindIPv4Proxy;
            }
        }

        /// <summary>
        /// Gets whether or not any of the device IPV6 connections have detected that they are behind a
        /// proxy.
        /// </summary>
        public bool BehindIPv6Proxy
        {
            get
            {
                return m_nListUtil.BehindIPv6Proxy;
            }
        }

        /// <summary>
        /// Gets whether or not any of the device IPV4 connections have been determined to be capable
        /// of reaching the internet.
        /// </summary>
        public bool HasIpv4InetConnection
        {
            get
            {
                return m_nListUtil.HasIPv4InetConnection;
            }
        }

        /// <summary>
        /// Gets whether or not any of the device IPV6 connections have been determined to be capable
        /// of reaching the internet.
        /// </summary>
        public bool HasIpv6InetConnection
        {
            get
            {
                return m_nListUtil.HasIPv6InetConnection;
            }
        }

        /// <summary>
        /// Private ctor since we're cheezy and use a singleton.
        /// </summary>
        private NetworkStatus()
        {
            m_nListUtil = PlatformTypes.New<INetworkInfo>();

            m_nListUtil.ConnectionStateChanged += () =>
            {
                ConnectionStateChanged?.Invoke();
            };
        }
    }
}
