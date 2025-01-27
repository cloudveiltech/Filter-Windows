/*
* Copyright � 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using Filter.Platform.Common.Net;
using Filter.Platform.Common.Util;
using NETWORKLIST;
using NLog;
using System;
using System.Runtime.InteropServices.ComTypes;

namespace CloudVeil.Core.Windows.Util.Net
{
    /// <summary>
    /// Internal class that hooks into the NETWORKLIST COM object for analyzing aspects of our
    /// network connections.
    /// </summary>
    internal class NetworkListUtil : INetworkListManagerEvents, INetworkEvents, INetworkConnectionEvents, INetworkInfo
    {
        private Logger logger;

        public event ConnectionStateChangeHandler ConnectionStateChanged;

        private enum NLM_INTERNET_CONNECTIVITY : uint
        {
            NLM_INTERNET_CONNECTIVITY_WEBHIJACK = 0x1,
            NLM_INTERNET_CONNECTIVITY_PROXIED = 0x2,
            NLM_INTERNET_CONNECTIVITY_CORPORATE = 0x4
        };

        /// <summary>
        /// Iterates over all IPV4 connections and checks to see if the
        /// NLM_INTERNET_CONNECTIVITY_WEBHIJACK flag is set for that connnection. Will return true on
        /// the very first connection that has this flag set. False if no connections have this flag set.
        /// </summary>
        public bool BehindIPv4CaptivePortal
        {
            get
            {
                foreach(INetwork nw in networkListManager.GetNetworks(NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_CONNECTED))
                {
                    try
                    {
                        INetwork network = networkListManager.GetNetwork(nw.GetNetworkId());
                        IPropertyBag nwp = (IPropertyBag)network;

                        object resv4;
                        nwp.RemoteRead("NA_InternetConnectivityV4", out resv4, null, 0, null);
                        NLM_INTERNET_CONNECTIVITY resv4Cast = (NLM_INTERNET_CONNECTIVITY)resv4;

                        if(resv4Cast.HasFlag(NLM_INTERNET_CONNECTIVITY.NLM_INTERNET_CONNECTIVITY_WEBHIJACK))
                        {
                            return true;
                        }
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Iterates over all IPV6 connections and checks to see if the
        /// NLM_INTERNET_CONNECTIVITY_WEBHIJACK flag is set for that connnection. Will return true on
        /// the very first connection that has this flag set. False if no connections have this flag set.
        /// </summary>
        public bool BehindIPv6CaptivePortal
        {
            get
            {
                foreach(INetwork nw in networkListManager.GetNetworks(NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_CONNECTED))
                {
                    try
                    {
                        INetwork network = networkListManager.GetNetwork(nw.GetNetworkId());
                        IPropertyBag nwp = (IPropertyBag)network;

                        object resv6;
                        nwp.RemoteRead("NA_InternetConnectivityV6", out resv6, null, 0, null);
                        NLM_INTERNET_CONNECTIVITY resv6Cast = (NLM_INTERNET_CONNECTIVITY)resv6;

                        if(resv6Cast.HasFlag(NLM_INTERNET_CONNECTIVITY.NLM_INTERNET_CONNECTIVITY_WEBHIJACK))
                        {
                            return true;
                        }
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Iterates over all IPV4 connections and checks to see if the
        /// NLM_INTERNET_CONNECTIVITY_PROXIED flag is set for that connnection. Will return true on
        /// the very first connection that has this flag set. False if no connections have this flag set.
        /// </summary>
        public bool BehindIPv4Proxy
        {
            get
            {
                foreach(INetwork nw in networkListManager.GetNetworks(NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_CONNECTED))
                {
                    try
                    {
                        INetwork network = networkListManager.GetNetwork(nw.GetNetworkId());
                        IPropertyBag nwp = (IPropertyBag)network;

                        object resv4;
                        nwp.RemoteRead("NA_InternetConnectivityV4", out resv4, null, 0, null);
                        NLM_INTERNET_CONNECTIVITY resv4Cast = (NLM_INTERNET_CONNECTIVITY)resv4;

                        if(resv4Cast.HasFlag(NLM_INTERNET_CONNECTIVITY.NLM_INTERNET_CONNECTIVITY_PROXIED))
                        {
                            return true;
                        }
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Iterates over all IPV6 connections and checks to see if the
        /// NLM_INTERNET_CONNECTIVITY_PROXIED flag is set for that connnection. Will return true on
        /// the very first connection that has this flag set. False if no connections have this flag set.
        /// </summary>
        public bool BehindIPv6Proxy
        {
            get
            {
                foreach(INetwork nw in networkListManager.GetNetworks(NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_CONNECTED))
                {
                    try
                    {
                        INetwork network = networkListManager.GetNetwork(nw.GetNetworkId());
                        IPropertyBag nwp = (IPropertyBag)network;

                        object resv6;
                        nwp.RemoteRead("NA_InternetConnectivityV6", out resv6, null, 0, null);
                        NLM_INTERNET_CONNECTIVITY resv6Cast = (NLM_INTERNET_CONNECTIVITY)resv6;

                        if(resv6Cast.HasFlag(NLM_INTERNET_CONNECTIVITY.NLM_INTERNET_CONNECTIVITY_PROXIED))
                        {
                            return true;
                        }
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Iterates over all IPV4 connections and checks to see if the
        /// NLM_CONNECTIVITY_IPV4_INTERNET flag is set for that connnection, which means that Windows
        /// has determined that the internet is reachable from the connection in question. Will
        /// return true on the very first connection that has this flag set. False if no connections
        /// have this flag set, which means no internet detected.
        /// </summary>
        public bool HasIPv4InetConnection
        {
            get
            {
                foreach(INetwork nw in networkListManager.GetNetworks(NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_CONNECTED))
                {
                    try
                    {
                        if(nw.GetConnectivity().HasFlag(NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV4_INTERNET))
                        {
                            return true;
                        }
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Iterates over all IPV6 connections and checks to see if the
        /// NLM_CONNECTIVITY_IPV6_INTERNET flag is set for that connnection, which means that Windows
        /// has determined that the internet is reachable from the connection in question. Will
        /// return true on the very first connection that has this flag set. False if no connections
        /// have this flag set, which means no internet detected.
        /// </summary>
        public bool HasIPv6InetConnection
        {
            get
            {
                foreach(INetwork nw in networkListManager.GetNetworks(NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_CONNECTED))
                {
                    try
                    {
                        if(nw.GetConnectivity().HasFlag(NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV6_INTERNET))
                        {
                            return true;
                        }
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(logger, e);
                    }
                }

                return false;
            }
        }

        private INetworkListManager networkListManager;
        private IConnectionPoint connectionPoint;
        private int cookie = 0;

        public NetworkListUtil()
        {
            logger = LoggerUtil.GetAppWideLogger();

            networkListManager = new NETWORKLIST.NetworkListManager();
            ConnectToNetworkListManagerEvents();
        }

        /// <summary>
        /// Bit of a hack because of our singleton but we want to make sure
        /// we disconnect from COM stuff.
        /// </summary>
        ~NetworkListUtil()
        {   
            DisconnectFromNetworkListManagerEvents();
        }

        private void ConnectToNetworkListManagerEvents()
        {
            IConnectionPointContainer icpc = (IConnectionPointContainer)networkListManager;
            Guid typeGuid = typeof(INetworkListManagerEvents).GUID;
            icpc.FindConnectionPoint(ref typeGuid, out connectionPoint);
            connectionPoint.Advise(this, out cookie);
        }

        private void DisconnectFromNetworkListManagerEvents()
        {
            connectionPoint.Unadvise(cookie);
        }

        public void ConnectivityChanged(NLM_CONNECTIVITY newConnectivity)
        {
            ConnectionStateChanged?.Invoke();
        }

        public void NetworkConnectivityChanged(Guid networkId, NLM_CONNECTIVITY newConnectivity)
        {
            ConnectionStateChanged?.Invoke();            
        }

        public void NetworkPropertyChanged(Guid networkId, NLM_NETWORK_PROPERTY_CHANGE Flags)
        {
            // Don't care.
        }

        public void NetworkConnectionConnectivityChanged(Guid connectionId, NLM_CONNECTIVITY newConnectivity)
        {
            // Don't care.
        }

        public void NetworkConnectionPropertyChanged(Guid connectionId, NLM_CONNECTION_PROPERTY_CHANGE Flags)
        {
            // Don't care.
        }

        public void NetworkAdded(Guid networkId)
        {
            // Don't care.
        }

        public void NetworkDeleted(Guid networkId)
        {
            // Don't care.
        }
    }
}