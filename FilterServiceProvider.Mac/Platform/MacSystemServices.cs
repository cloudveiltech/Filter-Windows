// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Filter.Platform.Common.Util;
using FilterProvider.Common.Data;
using FilterProvider.Common.Platform;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.Models;

namespace FilterServiceProvider.Mac.Platform
{
    public class MacSystemServices : ISystemServices
    {
        public MacSystemServices()
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        private NLog.Logger logger;

        public event EventHandler SessionEnding;

        public void DisableInternet()
        {
            // TODO: Can we disable internet? I don't think so.
            // This requires kernel access, which our app does not currently have.
        }

        public void EnableInternet()
        {
            // TODO: Can we disable internet?
        }

        public void EnsureFirewallAccess()
        {
            // TODO: No firewall access needs to be ensured? Maybe we could use this to
            // enable firewall access and use user-mode firewall as a way to disable internet.
        }

        public void RunProtectiveServices()
        {
            // TODO: Think about what we can do for protective services in macOS.
        }

        public ProxyServer StartProxyServer(ProxyConfiguration config)
        {
            var endPointHttp = new ExplicitProxyEndPoint(IPAddress.Any, 14300, true);
            var endPointHttps = new ExplicitProxyEndPoint(IPAddress.Any, 14301, true);

            // TODO: This trusting might need to be done with our own custom code.
            ProxyServer proxyServer = new ProxyServer(false, false);

            proxyServer.EnableConnectionPool = true;

            proxyServer.EnableTcpServerConnectionPrefetch = false;

            proxyServer.CertificateManager.CreateRootCertificate(false);
            proxyServer.CertificateManager.TrustRootCertificate();

            proxyServer.CertificateManager.CertificateEngine = Titanium.Web.Proxy.Network.CertificateEngine.BouncyCastle;

            proxyServer.AddEndPoint(endPointHttp);
            proxyServer.AddEndPoint(endPointHttps);

            proxyServer.BeforeRequest += config.BeforeRequest;
            proxyServer.BeforeResponse += config.BeforeResponse;
            proxyServer.AfterResponse += config.AfterResponse;

            proxyServer.ExceptionFunc += LogException;

            IntPtr appleCertificate = MacTrustManager.GetFromKeychain("org.cloudveil.filterserviceprovider");

            if(appleCertificate == IntPtr.Zero)
            {
                // Keychain certificate doesn't exist? Create one here and add it to our keychain.

                proxyServer.CertificateManager.EnsureRootCertificate(false, false);

                byte[] appleCertificateBytes = proxyServer.CertificateManager.RootCertificate.Export(X509ContentType.Cert, "password");
                appleCertificate = MacTrustManager.AddToKeychain(appleCertificateBytes, appleCertificateBytes.Length, "org.cloudveil.filterserviceprovider");
            }
            else
            {
                // Keychain certificate exists. Get the bytes and import to our root certificate.
                byte[] appleCertificateBytes = MacTrustManager.GetAppleCertificateBytes(appleCertificate);
                proxyServer.CertificateManager.RootCertificate = new X509Certificate2(appleCertificateBytes, "password");
            }

            MacTrustManager.EnsureCertificateTrust(appleCertificate);

            MacTrustManager.ReleaseCertificate(appleCertificate);
            appleCertificate = IntPtr.Zero;

            proxyServer.Start();

            MacProxyEnforcement.SetProxy("localhost", 14300, 14301);

            return proxyServer;
        }

        private void LogException(Exception exception)
        {
            logger.Error("TITANIUM.WEB.PROXY ERROR");
            LoggerUtil.RecursivelyLogException(logger, exception);
        }
    }
}
