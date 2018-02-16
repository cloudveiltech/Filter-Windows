using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Deployment.WindowsInstaller;
using System.IO;
using System.Diagnostics;
using System.ServiceProcess;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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
            Process.Start(Path.Combine(mainFolder, "CloudVeil.exe"));
            var filterServiceAssemblyPath = Path.Combine(mainFolder, "FilterServiceProvider.exe");

            // TODO: Not sure if uninstall command is needed any more? Seems like there was a conversation about this not being needed anymore.
            var uninstallStartInfo = new ProcessStartInfo(filterServiceAssemblyPath);
            uninstallStartInfo.Arguments = "Uninstall";
            uninstallStartInfo.UseShellExecute = false;
            uninstallStartInfo.CreateNoWindow = true;
            var uninstallProc = Process.Start(uninstallStartInfo);
            uninstallProc.WaitForExit();

            var installStartInfo = new ProcessStartInfo(filterServiceAssemblyPath);
            installStartInfo.Arguments = "Install";
            installStartInfo.UseShellExecute = false;
            installStartInfo.CreateNoWindow = true;

            var installProc = Process.Start(installStartInfo);
            installProc.WaitForExit();

            string restartFlagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "restart.flag");

            // 'norestart' not defined in Context.Parameters, so we can't use Context.IsParameterTrue.
            // This file gets defined by the filter service before shutting down.
            if (File.Exists(restartFlagPath))
            {
                File.Delete(restartFlagPath);
                InitiateShutdown(null, null, 0, (uint)(ShutdownFlags.SHUTDOWN_FORCE_OTHERS | ShutdownFlags.SHUTDOWN_RESTART | ShutdownFlags.SHUTDOWN_RESTARTAPPS), 0);
            }

            EnsureStartServicePostInstall(filterServiceAssemblyPath);

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
