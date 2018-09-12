/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace InstallGuard
{
    [RunInstaller(true)]
    public partial class UninstallCommand : System.Configuration.Install.Installer
    {
        public UninstallCommand()
        {
            InitializeComponent();
        }

        protected override void OnBeforeUninstall(IDictionary savedState)
        {
            base.OnBeforeUninstall(savedState);
            
            var installDir = Path.GetDirectoryName(typeof(UninstallCommand).Assembly.Location);

            foreach(var proc in Process.GetProcesses())
            {
                string mainModulePath = string.Empty;
                try
                {
                    if(proc.Id == Process.GetCurrentProcess().Id)
                    {
                        continue;
                    }

                    mainModulePath = proc.MainModule.FileName;
                }
                catch { }

                if(mainModulePath != null && mainModulePath.Length > 0 && mainModulePath.IndexOf(installDir) != -1)
                {
                    throw new Exception("Cannot uninstall the filter while the filter is running. Please exit the filter, or contact your support provider for assistance.");
                }
            }
        }
    }
}