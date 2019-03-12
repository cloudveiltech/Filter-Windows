/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.Core.Windows.Util;
using CitadelCore.Windows.Diversion;
using CitadelService.Services;
using Filter.Platform.Common;
using Filter.Platform.Common.Util;
using Filter.Platform.Common.Extensions;
using FilterProvider.Common.Data;
using FilterProvider.Common.Platform;
using FilterProvider.Common.Proxy;
using FilterProvider.Common.Proxy.Certificate;
using Microsoft.Win32;
using murrayju.ProcessExtensions;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WindowsFirewallHelper;

namespace CitadelService.Platform
{
    public class WindowsSystemServices : ISystemServices
    {
        public event EventHandler SessionEnding;

        private NLog.Logger m_logger;

        private FilterServiceProvider m_provider;

        private X509Certificate2 rootCert;
        public X509Certificate2 RootCertificate => rootCert;

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

        private void trustRootCertificate(X509Certificate2 cert)
        {
            var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);

            // Remove any certificates with this cert's subject name before installing this one.
            foreach(var existingCert in store.Certificates)
            {
                if(existingCert.SubjectName.Format(false) == cert.SubjectName.Format(false))
                {
                    store.Remove(existingCert);
                }
            }

            store.Add(cert);
        }

        public IProxyServer StartProxyServer(ProxyConfiguration config)
        {
            CommonProxyServer server = new CommonProxyServer();

            var paths = PlatformTypes.New<IPathProvider>();

            string certPath = paths.GetPath(@"rootCertificate.pem");
            string keyPath = paths.GetPath(@"rootPrivateKey.pem");

            BCCertificateMaker certMaker = new BCCertificateMaker();

            AsymmetricCipherKeyPair pair = BCCertificateMaker.CreateKeyPair(2048);

            using (StreamWriter writer = new StreamWriter(new FileStream(keyPath, FileMode.Create, FileAccess.Write)))
            {
                BCCertificateMaker.ExportPrivateKey(pair.Private, writer);
            }

            X509Certificate2 cert = certMaker.MakeCertificate(config.AuthorityName, true, null, pair);

            using (StreamWriter writer = new StreamWriter(new FileStream(certPath, FileMode.Create, FileAccess.Write)))
            {
                BCCertificateMaker.ExportDotNetCertificate(cert, writer);
            }

            trustRootCertificate(cert);
            rootCert = cert;

            server.Init(14300, 14301, certPath, keyPath);

            server.BeforeRequest += config.BeforeRequest;
            server.BeforeResponse += config.BeforeResponse;

            /*proxyServer.EnableConnectionPool = true;

            // TCP server connection prefetch doesn't work with our reverse proxy setup.
            proxyServer.EnableTcpServerConnectionPrefetch = false;

            proxyServer.CertificateManager.CreateRootCertificate(false);

            proxyServer.CertificateManager.TrustRootCertificate();*/

            //proxyServer.CertificateManager.CertificateEngine = CertificateEngine.BouncyCastle;

            //proxyServer.CertificateManager.EnsureRootCertificate(true, true);
            server.Start();
            //proxyServer.Start();

            WindowsDiverter diverter = new WindowsDiverter(14300, 14301, 14300, 14301);

            diverter.ConfirmDenyFirewallAccess = m_provider.OnAppFirewallCheck;

            diverter.Start(0);

            OnStartProxy?.Invoke(this, new EventArgs());

            return server;
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

        private bool TryGetGuiFullPath(out string fullGuiExePath)
        {
            var allFilesWhereIam = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.exe", SearchOption.TopDirectoryOnly);

            try
            {
                // Get all exe files in the same dir as this service executable.
                foreach (var exe in allFilesWhereIam)
                {
                    try
                    {
                        m_logger.Info("Checking exe : {0}", exe);
                        // Try to get the exe file metadata.
                        var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);

                        // If our description notes that it's a GUI...
                        if (fvi != null && fvi.FileDescription != null && fvi.FileDescription.IndexOf("GUI", StringComparison.OrdinalIgnoreCase) != -1)
                        {
                            fullGuiExePath = exe;
                            return true;
                        }
                    }
                    catch (Exception le)
                    {
                        LoggerUtil.RecursivelyLogException(m_logger, le);
                    }
                }
            }
            catch (Exception e)
            {
                m_logger.Error("Error enumerating sibling files.");
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }

            fullGuiExePath = string.Empty;
            return false;
        }

        public void KillAllGuis()
        {
            try
            {
                string guiExePath;
                if (TryGetGuiFullPath(out guiExePath))
                {
                    foreach (var proc in Process.GetProcesses())
                    {
                        try
                        {
                            if (proc.MainModule.FileName.OIEquals(guiExePath))
                            {
                                proc.Kill();
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception e)
            {
                m_logger.Error("Error enumerating processes when trying to kill all GUI instances.");
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        /// <summary>
        /// Attempts to determine which neighbour application is the GUI and then, if it is not
        /// running already as a user process, start the GUI. This should be used in situations like
        /// when we need to ask the user to authenticate.
        /// </summary>
        public void EnsureGuiRunning(bool runInTray = false)
        {
            var allFilesWhereIam = Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.exe", SearchOption.TopDirectoryOnly);

            try
            {
                string guiExePath;
                if (TryGetGuiFullPath(out guiExePath))
                {
                    m_logger.Info("Starting external GUI executable : {0}", guiExePath);

                    if (runInTray)
                    {
                        var sanitizedArgs = "\"" + Regex.Replace("/StartMinimized", @"(\\+)$", @"$1$1") + "\"";
                        var sanitizedPath = "\"" + Regex.Replace(guiExePath, @"(\\+)$", @"$1$1") + "\"" + " " + sanitizedArgs;

                        ProcessExtensions.StartProcessAsCurrentUser(null, sanitizedPath);
                    }
                    else
                    {
                        ProcessExtensions.StartProcessAsCurrentUser(guiExePath);
                    }


                    return;
                }
            }
            catch (Exception e)
            {
                m_logger.Error("Error enumerating all files.");
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
        }

        public event EventHandler OnStartProxy;
    }
}
