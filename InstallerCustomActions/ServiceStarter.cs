/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Deployment.WindowsInstaller;
using System.IO;
using System.Diagnostics;
using System.ServiceProcess;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using murrayju.ProcessExtensions;

namespace InstallerCustomActions
{
    public enum ShutdownFlags
    {
        SHUTDOWN_FORCE_OTHERS = 0x1,
        SHUTDOWN_FORCE_SELF = 0x2,
        SHUTDOWN_RESTART = 0x4,
        SHUTDOWN_NOREBOOT = 0x10,
        SHUTDOWN_GRACE_OVERRIDE = 0x20,
        SHUTDOWN_INSTALL_UPDATES = 0x40,
        SHUTDOWN_RESTARTAPPS = 0x80,
        SHUTDOWN_HYBRID = 0x200
    }

    public class ServiceStarter
    {
        [CustomAction]
        public static ActionResult StartServicePostInstall(Session session)
        {
            string mainFolder = session.CustomActionData["TargetDirectory"];
            session.Log($"Main folder = {mainFolder}");

            Directory.SetCurrentDirectory(mainFolder);

            var filterServiceAssemblyPath = Path.Combine(mainFolder, "FilterServiceProvider.exe");

            try
            {
                // TODO: Not sure if uninstall command is needed any more? Seems like there was a conversation about this not being needed anymore.
                var uninstallStartInfo = new ProcessStartInfo(filterServiceAssemblyPath);
                uninstallStartInfo.Arguments = "Uninstall";
                uninstallStartInfo.UseShellExecute = false;
                uninstallStartInfo.CreateNoWindow = true;
                var uninstallProc = Process.Start(uninstallStartInfo);
                uninstallProc.WaitForExit();
            }
            catch (Exception ex)
            {
                session.Log("ERROR: Exception occurred while running service uninstall command. {0}", ex);
            }

            try
            {
                var installStartInfo = new ProcessStartInfo(filterServiceAssemblyPath);
                installStartInfo.Arguments = "Install";
                installStartInfo.UseShellExecute = false;
                installStartInfo.CreateNoWindow = true;

                var installProc = Process.Start(installStartInfo);
                installProc.WaitForExit();
            }
            catch (Exception ex)
            {
                session.Log("ERROR: Exception occurred while running service install command. {0}", ex);
            }

            var imageFilterAssemblyPath = Path.Combine(mainFolder, "ImageFilter\\ImageFilter.exe");

            try
            {
                // TODO: Not sure if uninstall command is needed any more? Seems like there was a conversation about this not being needed anymore.
                var uninstallStartInfo = new ProcessStartInfo(imageFilterAssemblyPath);
                uninstallStartInfo.Arguments = "Uninstall";
                uninstallStartInfo.UseShellExecute = false;
                uninstallStartInfo.CreateNoWindow = true;
                var uninstallProc = Process.Start(uninstallStartInfo);
                uninstallProc.WaitForExit();
            }
            catch (Exception ex)
            {
                session.Log("ERROR: Exception occurred while running service uninstall command. {0}", ex);
            }

            try
            {
                var installStartInfo = new ProcessStartInfo(imageFilterAssemblyPath);
                installStartInfo.Arguments = "Install";
                installStartInfo.UseShellExecute = false;
                installStartInfo.CreateNoWindow = true;

                var installProc = Process.Start(installStartInfo);
                installProc.WaitForExit();
            }
            catch (Exception ex)
            {
                session.Log("ERROR: Exception occurred while running service install command. {0}", ex);
            }

            string restartFlagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "restart.flag");

            // 'norestart' not defined in Context.Parameters, so we can't use Context.IsParameterTrue.
            // This file gets defined by the filter service before shutting down.
            if (File.Exists(restartFlagPath))
            {
                File.Delete(restartFlagPath);
                InitiateShutdown(null, null, 0, (uint)(ShutdownFlags.SHUTDOWN_FORCE_OTHERS | ShutdownFlags.SHUTDOWN_RESTART | ShutdownFlags.SHUTDOWN_RESTARTAPPS), 0);
            }

            EnsureStartServicePostInstall(filterServiceAssemblyPath);
            EnsureStartServicePostInstall(imageFilterAssemblyPath);

            // Wait until after service is started to start GUI so that we don't get a UAC prompt from FilterAgent.Windows.
            string appPath = Path.Combine(mainFolder, "CloudVeil.exe");
            ProcessExtensions.StartProcessAsCurrentUser(appPath, null, mainFolder, true);

            return ActionResult.Success;
        }

        private static void EnsureStartServicePostInstall(string filterServiceAssemblyPath)
        {
            // XXX TODO - This is a dirty hack.
            int tries = 0;

            while (!TryStartService(filterServiceAssemblyPath) && tries < 20)
            {
                Task.Delay(200).Wait();
                ++tries;
            }
        }

        private static bool TryStartService(string filterServiceAssemblyPath)
        {
            try
            {
                TimeSpan timeout = TimeSpan.FromSeconds(60);

                foreach (var service in ServiceController.GetServices())
                {
                    if (service.ServiceName.IndexOf(Path.GetFileNameWithoutExtension(filterServiceAssemblyPath), StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        if (service.Status == ServiceControllerStatus.StartPending)
                        {
                            service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                        }

                        if (service.Status != ServiceControllerStatus.Running)
                        {
                            service.Start();
                            service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern UInt32 InitiateShutdown(
            string lpMachineName,
            string lpMessage,
            UInt32 dwGracePeriod,
            UInt32 dwShutdownFlags,
            UInt32 dwReason);
    }
}
