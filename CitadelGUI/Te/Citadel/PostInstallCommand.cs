/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace Te.Citadel
{
    // XXX TODO There are some steps you need to take for the post-install exec to work correctly
    // when making a 64 bit MSI installer. You need to modify the 64 bit MSI as described at the
    // following locations:
    // http://stackoverflow.com/questions/10275106/badimageformatexception-x64-issue/10281533#10281533 http://stackoverflow.com/questions/5475820/system-badimageformatexception-when-installing-program-from-vs2010-installer-pro/6797989#6797989
    //
    // Just in case. Steps are: First, ensure you have Orca installed. Run Orca and open your
    // project's MSI folder Select the Binary table Double click the cell [Binary Data] for the
    // record InstallUtil Make sure "Read binary from filename" is selected Click the Browse button
    // Browse to C:\Windows\Microsoft.NET\Framework64\v4.0.30319 Select InstallUtilLib.dll Click the
    // Open button and then the OK button
    [RunInstaller(true)]
    public partial class PostInstallCommand : System.Configuration.Install.Installer
    {
        public PostInstallCommand()
        {
            InitializeComponent();
        }

        public override void Commit(IDictionary savedState)
        {   
            base.Commit(savedState);
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            System.Diagnostics.Process.Start(Assembly.GetExecutingAssembly().Location);

            var filterServiceAssemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "FilterServiceProvider.exe");

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

            EnsureStartServicePostInstall(filterServiceAssemblyPath);

            Environment.Exit(0);

            base.Dispose();
        }

        private void EnsureStartServicePostInstall(string filterServiceAssemblyPath)
        {
            // XXX TODO - This is a dirty hack.
            int tries = 0;

            while(!TryStartService(filterServiceAssemblyPath) && tries < 20)
            {
                Task.Delay(200).Wait();
                ++tries;
            }
        }

        private bool TryStartService(string filterServiceAssemblyPath)
        {
            try
            {
                TimeSpan timeout = TimeSpan.FromSeconds(60);

                foreach(var service in ServiceController.GetServices())
                {
                    if(service.ServiceName.IndexOf(Path.GetFileNameWithoutExtension(filterServiceAssemblyPath), StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        if(service.Status == ServiceControllerStatus.StartPending)
                        {
                            service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                        }

                        if(service.Status != ServiceControllerStatus.Running)
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
    }
}