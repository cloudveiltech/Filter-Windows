/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Filter.Platform.Common.Extensions;
using Filter.Platform.Common.Util;
using FilterProvider.Common.Configuration;
using DNS.Client;
using DNS.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FilterProvider.Common.Platform;
using Filter.Platform.Common;
using Filter.Platform.Common.Net;
using CloudVeil;

namespace FilterProvider.Common.Util
{
    public delegate void CaptivePortalModeHandler(bool isCaptivePortal, bool isActive);
    public delegate void DnsEnforcementHandler(bool isEnforcementActive);
    public delegate void DnsChangeEventHandler(bool flushSuccessful);

    internal class DnsEnforcement
    {
        /// <summary>
        /// This timer is used to monitor local NIC cards and enforce DNS settings when they are
        /// configured in the application config.
        /// </summary>
        private Timer m_dnsEnforcementTimer;

        internal DnsEnforcement(IPolicyConfiguration configuration, NLog.Logger logger)
        {
            m_logger = logger;
            m_policyConfiguration = configuration;
            m_platformDns = PlatformTypes.New<IPlatformDns>();
        }

        private object m_dnsEnforcementLock = new object();
        private NLog.Logger m_logger;
        private IPolicyConfiguration m_policyConfiguration;
        private IPlatformDns m_platformDns;

        #region DnsEnforcement.Enforce
        
        IPAddress lastPrimary = null;
        IPAddress lastSecondary = null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="enableDnsFiltering">If true, this function enables DNS filtering with entries in the configuration.</param>
        public void TryEnforce(bool sendDnsChangeEvents, bool enableDnsFiltering = true)
        {
            m_logger.Info("TryEnforce DNS {0}, {1}", sendDnsChangeEvents, enableDnsFiltering);

            lock (m_dnsEnforcementLock)
            {
                try
                {
                    if(!enableDnsFiltering)
                    {
                        if (m_policyConfiguration.Configuration == null)
                        {
                            EventHandler fn = null;

                            fn = (sender, e) =>
                            {
                                this.SetDnsToDhcp(sendDnsChangeEvents);
                                m_policyConfiguration.OnConfigurationLoaded -= fn;
                            };

                            m_policyConfiguration.OnConfigurationLoaded += fn;
                        }
                        else
                        {
                            SetDnsToDhcp(sendDnsChangeEvents);
                        }
                    }
                    else
                    {
                        IPAddress primaryDns = null;
                        IPAddress secondaryDns = null;

                        var cfg = m_policyConfiguration.Configuration;

                        // Check if any DNS servers are defined, and if so, set them.
                        if (cfg != null && StringExtensions.Valid(cfg.PrimaryDns))
                        {
                            IPAddress.TryParse(cfg.PrimaryDns.Trim(), out primaryDns);
                        }

                        if (cfg != null && StringExtensions.Valid(cfg.SecondaryDns))
                        {
                            IPAddress.TryParse(cfg.SecondaryDns.Trim(), out secondaryDns);
                        }

                        if (primaryDns != null || secondaryDns != null)
                        {
                            bool ranUpdate = m_platformDns.SetDnsForAllInterfaces(primaryDns, secondaryDns);

                            if(areDnsServersChanging(primaryDns, secondaryDns))
                            {
                                OnDnsChanging(sendDnsChangeEvents);
                            }

                            lastPrimary = primaryDns;
                            lastSecondary = secondaryDns;
                        }
                        else
                        {
                            // Neither primary nor secondary DNS are set. Clear them for our users.
                            SetDnsToDhcp(sendDnsChangeEvents);
                            lastPrimary = null;
                            lastSecondary = null;
                        }
                    }
                }
                catch (Exception e)
                {
                    LoggerUtil.RecursivelyLogException(m_logger, e);
                }
            }
        }

        private bool isDnsServerChanged(IPAddress ip, IPAddress last)
        {
            if(ip == null && last == null)
            {
                return false;
            }

            if((ip == null && last != null) || (ip != null && last == null))
            {
                return true;
            }

            if(!ip.Equals(last))
            {
                return true;
            }

            return false;
        }

        private bool areDnsServersChanging(IPAddress primary, IPAddress secondary)
        {
            bool primaryChanged = isDnsServerChanged(primary, lastPrimary);
            bool secondaryChanged = isDnsServerChanged(secondary, lastSecondary);

            return primaryChanged || secondaryChanged;
        }

        public event DnsChangeEventHandler DnsChanged;

        private void OnDnsChanging(bool sendDnsChangeEvents)
        {
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo("ipconfig", "/flushdns");
            process.Start();
            process.Exited += (sender, e) =>
            {
                if (sendDnsChangeEvents)
                {
                    DnsChanged?.Invoke(process.ExitCode == 0);
                }
            };
        }

        private void SetDnsToDhcp(bool sendDnsChangeEvents)
        {
            m_logger.Info("Setting DNS to DHCP.");

            // Is configuration loaded?
            IPAddress primaryDns = null;
            IPAddress secondaryDns = null;

            var cfg = m_policyConfiguration.Configuration;

            // Check if any DNS servers are defined, and if so, set them.
            if (cfg != null && StringExtensions.Valid(cfg.PrimaryDns))
            {
                IPAddress.TryParse(cfg.PrimaryDns.Trim(), out primaryDns);
            }

            if (cfg != null && StringExtensions.Valid(cfg.SecondaryDns))
            {
                IPAddress.TryParse(cfg.SecondaryDns.Trim(), out secondaryDns);
            }

            m_logger.Info("Stored DNS in configuration {0}, {1}", primaryDns, secondaryDns);

            if (lastPrimary == null && lastSecondary == null && primaryDns == null && secondaryDns == null)
            {
                // Don't mangle with the user's DNS settings, since our filter isn't controlling them.
                m_logger.Info("Primary and Secondary DNS servers are both null.");

                return;
            }

            if(areDnsServersChanging(primaryDns, secondaryDns))
            {
                OnDnsChanging(sendDnsChangeEvents);
            }

            m_platformDns.SetDnsForAllInterfacesToDHCP();
        }
        #endregion

        #region DnsEnforcement.Decision
        /// <summary>
        /// Detects whether the user is behind a captive portal.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> IsBehindCaptivePortal()
        {
            bool active = await IsCaptivePortalActive();

            if (active)
            {
                m_logger.Info("Active captive portal detected");

                CaptivePortalHelper.Default.OnCaptivePortalDetected();
                OnCaptivePortalMode?.Invoke(true, true);
                return active;
            }
            else
            {
                bool ret = CaptivePortalHelper.Default.IsCurrentNetworkCaptivePortal();
                if(ret) m_logger.Info("It looks like we're on a captive portal network, but you have internet access.");

                OnCaptivePortalMode?.Invoke(ret, active);
                return ret;
            }
        }

        private DateTime lastDnsCheck = DateTime.MinValue;
        private bool lastDnsResult = true;

        public void InvalidateDnsResult()
        {
            lastDnsCheck = DateTime.MinValue;
        }

        /// <summary>
        /// Detects whether our DNS servers are down.
        /// 
        /// This one's a little sticky because we don't know whether internet is down for sure.
        /// I think it's easy enough to just assume that if we can't reach our DNS servers we should probably flip the switch.
        /// 
        /// If first server checked is up, no more are checked, and so on.
        /// </summary>
        /// <returns>Returns true if at least one of the servers in the configuration returns a response or if there are none configured. Returns false if all servers tried do not return a response.</returns>
        public async Task<bool> IsDnsUp()
        {
            if(lastDnsCheck.AddMinutes(5) > DateTime.Now)
            {
                return lastDnsResult;
            }

            lastDnsCheck = DateTime.Now;

            bool ret = false;

            if(m_policyConfiguration.Configuration == null)
            {
                // We can't really make a decision on enforcement here, but just return true anyway.
                return true;
            }

            string primaryDns = m_policyConfiguration.Configuration.PrimaryDns;
            string secondaryDns = m_policyConfiguration.Configuration.SecondaryDns;

            if (string.IsNullOrWhiteSpace(primaryDns) && string.IsNullOrWhiteSpace(secondaryDns))
            {
                ret = true;
            }
            else
            {
                List<string> dnsSearch = new List<string>();
                if (!string.IsNullOrWhiteSpace(primaryDns))
                {
                    dnsSearch.Add(primaryDns);
                }

                if (!string.IsNullOrWhiteSpace(secondaryDns))
                {
                    dnsSearch.Add(secondaryDns);
                }

                int failedDnsServers = 0;

                foreach (string dnsServer in dnsSearch)
                {
                    try
                    {
                        DnsClient client = new DnsClient(dnsServer);

                        IList<IPAddress> ips = await client.Lookup("testdns.cloudveil.org");

                        if (ips != null && ips.Count > 0)
                        {
                            ret = true;
                            break;
                        }
                        else
                        {
                            failedDnsServers++;
                        }
                    }
                    catch (Exception ex)
                    {
                        failedDnsServers++;
                        m_logger.Error($"Failed to contact DNS server {dnsServer}");
                        LoggerUtil.RecursivelyLogException(m_logger, ex);
                    }
                }
            }

            lastDnsResult = ret;
            return ret;
        }

        /// <summary>
        /// Detects whether we are blocked by a captive portal and returns result accordingly.
        /// </summary>
        private async Task<bool> IsCaptivePortalActive()
        {
            if (!NetworkStatus.Default.HasIpv4InetConnection && !NetworkStatus.Default.HasIpv6InetConnection)
            {
                // No point in checking further if no internet available.
                try
                {
                    IPHostEntry entry = Dns.GetHostEntry("connectivitycheck.cloudveil.org");
                }
                catch (Exception ex)
                {
                    m_logger.Info("No DNS servers detected as up by captive portal.");
                    LoggerUtil.RecursivelyLogException(m_logger, ex);

                    return false;
                }

                // Did we get here? This probably means we have internet access, but captive portal may be blocking.
            }

            CaptivePortalDetected ret = checkCaptivePortalState();
            if (ret == CaptivePortalDetected.NoResponseReturned)
            {
                m_logger.Info("Captive Portal no response returned.");

                // If no response is returned, this may mean that 
                // a) the network is still initializing
                // b) we have no internet.
                // Schedule a Trigger() for 1.5 second in the future to handle (a)
                
                Task.Delay(1500).ContinueWith((task) =>
                {
                    Trigger();
                });

                return false;
            }
            else if (ret == CaptivePortalDetected.Yes)
            {
                m_logger.Info("Captive portal detected.");
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Checks URL specified at CloudVeil.CompileSecrets.ConnectivityCheck + "/ncsi.txt" for connectivity.
        /// </summary>
        /// <remarks>
        /// Windows 7 captive portal detection isn't perfect. Somehow in my testing, it got disabled on my test network.
        /// 
        /// Granted, a restart may fix it, but we're not going to ask our customers to do that in order to get their computer working on a captive portal.
        /// </remarks>
        /// <returns>true if captive portal.</returns>
        private CaptivePortalDetected checkCaptivePortalState()
        {
            if (NetworkStatus.Default.BehindIPv4CaptivePortal || NetworkStatus.Default.BehindIPv6CaptivePortal)
            {
                return CaptivePortalDetected.Yes;
            }

            // "Oh, you want to depend on Windows captive portal detection? Haha nope!" -- Boingo Wi-FI
            // Some captive portals indeed let msftncsi.com through and thoroughly break windows captive portal detection.
            // BWI airport wifi is one of them.
            switch(ConnectivityCheck.IsAccessible())
            {
                case ConnectivityCheck.Accessible.No: return CaptivePortalDetected.NoResponseReturned;
                case ConnectivityCheck.Accessible.Yes: return CaptivePortalDetected.No;
                case ConnectivityCheck.Accessible.UnexpectedResponse: return CaptivePortalDetected.Yes;
                default: return CaptivePortalDetected.No;
            }
        }
        #endregion

        // This region includes timers and other event functions in which to run decision functions
        #region DnsEnforcement.Triggers

        private bool isBehindCaptivePortal = false;

        public async void Trigger(bool sendDnsChangeEvents = false)
        {
            m_logger.Info("Triggering DNS Enforcement Code (sendDnsChangeEvents={0})", sendDnsChangeEvents);

            try
            {
                bool isDnsUp = await IsDnsUp();

                if(!isDnsUp)
                {
                    m_logger.Info("DNS is down.");

                    TryEnforce(sendDnsChangeEvents, enableDnsFiltering: false);
                    return;
                }

                bool isCaptivePortal = await IsBehindCaptivePortal();

                isBehindCaptivePortal = isCaptivePortal;
                m_logger.Info("DnsEnforcement isCaptivePortal = {0}", isCaptivePortal);

                TryEnforce(sendDnsChangeEvents, enableDnsFiltering: !isCaptivePortal && isDnsUp);
            }
            catch (Exception ex)
            {
                m_logger.Error("Failed to trigger DnsEnforcement");
                LoggerUtil.RecursivelyLogException(m_logger, ex);
            }

            SetupTimers();
        }

        public void SetupTimers()
        {
            int timerTime = isBehindCaptivePortal ? 5000 : 60000;

            lock(m_dnsEnforcementLock)
            {
                if (m_dnsEnforcementTimer == null)
                {
                    m_dnsEnforcementTimer = new Timer(TriggerTimer, null, timerTime, timerTime);
                }
                else
                {
                    m_dnsEnforcementTimer.Change(TimeSpan.FromMilliseconds(timerTime), TimeSpan.FromMilliseconds(timerTime));
                }
            }
            
        }

        public void OnNetworkChange(object sender, EventArgs e)
        {
            m_logger.Info("Network Change Detected");

            if (m_policyConfiguration.Configuration == null)
            {
                EventHandler fn = null;

                fn = (_s, args) =>
                {
                    Trigger();
                    m_policyConfiguration.OnConfigurationLoaded -= fn;
                };

                m_policyConfiguration.OnConfigurationLoaded += fn;
            }
            else
            {
                Trigger();
            }
        }

        #endregion

        #region DnsEnforcement.Events
        public event DnsEnforcementHandler OnDnsEnforcementUpdate;
        public event CaptivePortalModeHandler OnCaptivePortalMode;
        #endregion

        private void TriggerTimer(object state)
        {
            Trigger();
        }
    }
}
