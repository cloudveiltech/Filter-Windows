// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Filter.Platform.Common.Net;

namespace Filter.Platform.Mac
{
    public class MacNetworkInfo : INetworkInfo, IDisposable
    {
        [DllImport(Platform.NativeLib)]
        private static extern bool IsInternetReachable(string hostname);

        public MacNetworkInfo()
        {
            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
        }

        // These are always false for two reasons.
        // 1. macOS has no good OS-level API to determine whether we are connected to a captive portal.
        // 2. We have our own captive portal detection that runs when these are false. (See DnsEnforcement.cs in FilterProvider.Common/Util)
        public bool BehindIPv4CaptivePortal => false;
        public bool BehindIPv6CaptivePortal => false;

        // These are always false for the following reason.
        // We are using OS-level proxy settings to provide our filtering services, so it will always
        // look to us like we are behind a proxy.
        public bool BehindIPv4Proxy => false;
        public bool BehindIPv6Proxy => false;

        private bool? isInetConnectionReachable = null;
        private void checkReachable()
        {
            if(!isInetConnectionReachable.HasValue)
            {
                isInetConnectionReachable = IsInternetReachable("connectivitycheck.cloudveil.org");
            }
        }

        public bool HasIPv4InetConnection
        {
            get
            {
                checkReachable();
                return isInetConnectionReachable ?? false;
            }
        }

        public bool HasIPv6InetConnection => HasIPv4InetConnection;

        public event ConnectionStateChangeHandler ConnectionStateChanged;

        void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            ConnectionStateChanged?.Invoke();
        }

        void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            ConnectionStateChanged?.Invoke();
        }


        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    NetworkChange.NetworkAddressChanged -= NetworkChange_NetworkAddressChanged;
                    NetworkChange.NetworkAvailabilityChanged -= NetworkChange_NetworkAvailabilityChanged;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~MacNetworkInfo() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
