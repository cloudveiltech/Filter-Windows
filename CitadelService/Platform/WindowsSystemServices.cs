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
using FilterNativeWindows;
using Citadel.Core.Windows.WinAPI;
using Filter.Platform.Common.Data.Models;
using System.Reflection;
using CitadelService.Util;
using System.ComponentModel;

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

            server.Blacklisted += config.Blacklisted;
            server.Whitelisted += config.Whitelisted;

            server.Start();

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

        public void OpenUrlInSystemBrowser(Uri url)
        {
            string reportPath = url.ToString();

            m_logger.Info("Starting process: {0}", AppAssociationHelper.PathToDefaultBrowser);
            var sanitizedArgs = "\"" + Regex.Replace(reportPath, @"(\\+)$", @"$1$1") + "\"";

            var sanitizedPath = "\"" + Regex.Replace(AppAssociationHelper.PathToDefaultBrowser, @"(\\+)$", @"$1$1") + "\"" + " " + sanitizedArgs;
            ProcessExtensions.StartProcessAsCurrentUser(null, sanitizedPath);

            var cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            ProcessExtensions.StartProcessAsCurrentUser(cmdPath, string.Format("/c start \"{0}\"", reportPath));
        }

        private Version getCV4WVersion()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            AssemblyName name = assembly?.GetName();

            return name?.Version;
        }

        private string getIpConfigInfo()
        {
            Process p = new Process();
            string ipconfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ipconfig.exe");

            p.StartInfo = new ProcessStartInfo(ipconfigPath, "/all")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            p.Start();

            Task<string> standardErrorTask = p.StandardError.ReadToEndAsync();
            Task<string> standardOutputTask = p.StandardOutput.ReadToEndAsync();

            p.WaitForExit();

            StringBuilder b = new StringBuilder();

            string separator = new string('-', 80);

            string stderr = standardErrorTask.Result;
            string stdout = standardErrorTask.Result;
            if(stderr.Length > 0)
            {
                b.AppendLine("ipconfig stderr");
                b.AppendLine(separator);
                b.AppendLine(stderr);

                if(stdout.Length > 0)
                {
                    b.AppendLine(separator);
                }
            }

            if(stdout.Length > 0)
            {
                b.AppendLine("ipconfig stdout");
                b.AppendLine(separator);
                b.AppendLine(stdout);
            }

            return b.ToString();
        }

        private void printProcessToStringBuilder(StringBuilder sb, Process p)
        {
            string processName = "n/a";
            int pid = -1;
            bool? hasExited = false;
            DateTime? exitTime = null;
            DateTime? startTime = null;
            TimeSpan? totalProcessorTime = null;
            long workingSet = -1;

            // We cannot guarantee that any specific process is going to allow us access to its information.
            // So we must wrap each assignment from Process in a try-catch to attempt to glean as much information about
            // it as we can.
            try { processName = p.ProcessName; } catch (Exception) { }
            try { pid = p.Id; } catch (Exception) { }
            try { startTime = p.StartTime; } catch (Exception) { }
            try { totalProcessorTime = p.TotalProcessorTime; } catch (Exception) { }
            try { workingSet = p.WorkingSet64; } catch (Exception) { }

            string startTimeString = startTime != null ? startTime.Value.ToString() : "n/a";
            string totalProcessorTimeString = totalProcessorTime != null ? totalProcessorTime.ToString() : "n/a";

            string timeRunString = null;
            if(startTime == null)
            {
                timeRunString = "n/a";
            }
            else
            {
                TimeSpan timeRun = (hasExited == true && exitTime != null ? exitTime.Value : DateTime.Now) - startTime.Value;
                timeRunString = timeRun.ToString();
            }

            sb.AppendLine($"\t{processName} ({pid}), StartTime={startTimeString}, ProcTime={totalProcessorTimeString}, MemUsage={workingSet / 1024L}KB");
        }

        private string getRunningProcessesReport()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Running Processes Report");

            Process[] processes = Process.GetProcesses();

            foreach(Process p in processes)
            {
                bool hasExited = false;
                try
                {
                    hasExited = p.HasExited;
                }
                catch (Exception) { }

                if (!hasExited)
                {
                    printProcessToStringBuilder(sb, p);
                }
            }

            return sb.ToString();
        }

        public ComputerInfo GetComputerInfo()
        {
            string mainSeparator = new string('=', 80);

            StringBuilder text = new StringBuilder();

            try
            {
                Version currentVersion = getCV4WVersion();

                text.AppendLine($"CV4W {currentVersion.ToString(3)} diagnostics log");
                text.AppendLine(mainSeparator);
                text.AppendLine();
                text.AppendLine(getIpConfigInfo());
                text.AppendLine();
                text.AppendLine(mainSeparator);
                text.AppendLine();
                text.AppendLine(InstalledPrograms.BuildInstalledProgramsReport());
                text.AppendLine();
                text.AppendLine(mainSeparator);
                text.AppendLine();
                text.AppendLine(getRunningProcessesReport());
                text.AppendLine();
                text.AppendLine(mainSeparator);
                text.AppendLine("End of Diagnostics log");

                return new ComputerInfo()
                {
                    DiagnosticsText = text.ToString()
                };
            }
            catch(Exception ex)
            {
                if(text != null)
                {
                    text.AppendLine(mainSeparator);
                    text.AppendLine(ex.ToString());

                    return new ComputerInfo()
                    {
                        DiagnosticsText = text.ToString()
                    };
                }
                else
                {
                    return new ComputerInfo()
                    {
                        DiagnosticsText = ex.ToString()
                    };
                }
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
