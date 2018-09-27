/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.Core.Windows.Util;
using CitadelCore.Windows.Diversion;
using CitadelService.Services;
using Filter.Platform.Common.Util;
using FilterProvider.Common.Data;
using FilterProvider.Common.Platform;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy.Network;
using WindowsFirewallHelper;

namespace CitadelService.Platform
{
    public class WindowsSystemServices : ISystemServices
    {
        public event EventHandler SessionEnding;

        private NLog.Logger m_logger;

        private FilterServiceProvider m_provider;

        public WindowsSystemServices(FilterServiceProvider provider)
        {
            SystemEvents.SessionEnding += SystemEvents_SessionEnding;
            m_logger = LoggerUtil.GetAppWideLogger();

            m_provider = provider;
        }

        private void SystemEvents_SessionEnding(object sender, SessionEndingEventArgs e)
        {
            SessionEnding?.Invoke(sender, e);
        }

        public void EnsureFirewallAccess()
        {
            try
            {
                string thisProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                var thisAssembly = System.Reflection.Assembly.GetExecutingAssembly();

                // Get all existing rules matching our process name and destroy them.
                var myRules = FirewallManager.Instance.Rules.Where(r => r.Name.Equals(thisProcessName, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (myRules != null && myRules.Length > 0)
                {
                    foreach (var rule in myRules)
                    {
                        FirewallManager.Instance.Rules.Remove(rule);
                    }
                }

                // Create inbound/outbound firewall rules and add them.
                var inboundRule = FirewallManager.Instance.CreateApplicationRule(
                    FirewallProfiles.Domain | FirewallProfiles.Private | FirewallProfiles.Public,
                    thisProcessName,
                    WindowsFirewallHelper.FirewallAction.Allow, thisAssembly.Location
                );
                inboundRule.Direction = FirewallDirection.Inbound;

                FirewallManager.Instance.Rules.Add(inboundRule);

                var outboundRule = FirewallManager.Instance.CreateApplicationRule(
                    FirewallProfiles.Domain | FirewallProfiles.Private | FirewallProfiles.Public,
                    thisProcessName,
                    WindowsFirewallHelper.FirewallAction.Allow, thisAssembly.Location
                );
                outboundRule.Direction = FirewallDirection.Outbound;

                FirewallManager.Instance.Rules.Add(outboundRule);
            }
            catch (Exception e)
            {
                m_logger.Error("Error while attempting to configure firewall application exception.");
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        public void RunProtectiveServices()
        {
            ServiceSpawner.Instance.InitializeServices();
        }

        public ProxyServer StartProxyServer(ProxyConfiguration config)
        {
            var transparentEndPointHttp = new TransparentProxyEndPoint(IPAddress.Any, 14300, false)
            {

            };

            var transparentEndPointHttps = new TransparentProxyEndPoint(IPAddress.Any, 14301, true)
            {

            };

            ProxyServer proxyServer = new ProxyServer(true, true);

            proxyServer.EnableConnectionPool = false;

            // TCP server connection prefetch doesn't work with our reverse proxy setup.
            proxyServer.EnableTcpServerConnectionPrefetch = false;

            proxyServer.CertificateManager.CreateRootCertificate(false);

            proxyServer.CertificateManager.TrustRootCertificate();

            //proxyServer.CertificateManager.CertificateEngine = CertificateEngine.BouncyCastle;
            proxyServer.CertificateManager.CertificateEngine = CertificateEngine.BouncyCastle;

            proxyServer.AddEndPoint(transparentEndPointHttp);
            proxyServer.AddEndPoint(transparentEndPointHttps);

            proxyServer.BeforeRequest += config.BeforeRequest;
            proxyServer.BeforeResponse += config.BeforeResponse;
            proxyServer.AfterResponse += config.AfterResponse;

            proxyServer.ExceptionFunc += LogException;

            proxyServer.CertificateManager.EnsureRootCertificate(true, true);
            proxyServer.Start();

            WindowsDiverter diverter = new WindowsDiverter(14300, 14301, 14300, 14301);

            diverter.ConfirmDenyFirewallAccess = m_provider.OnAppFirewallCheck;

            diverter.Start(0);

            return proxyServer;
        }

        private void LogException(Exception exception)
        {
            m_logger.Error("TITANIUM.WEB.PROXY ERROR");
            LoggerUtil.RecursivelyLogException(m_logger, exception);
        }

        public void EnableInternet()
        {
            m_logger.Info("Enabling internet.");
            WFPUtility.EnableInternet();
        }

        public void DisableInternet()
        {
            m_logger.Info("Disabling internet.");
            WFPUtility.DisableInternet();
        }
    }
}
