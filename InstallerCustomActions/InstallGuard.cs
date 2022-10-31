/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Microsoft.Deployment.WindowsInstaller;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstallerCustomActions
{
    public class InstallGuard
    {

        [CustomAction]
        public static ActionResult GuardInstall(Session session)
        {
            Debugger.Break();
            try
            {
                string installDir = null;
                if (!session.CustomActionData.TryGetValue("TargetDirectory", out installDir))
                {
                    session.Log($"InstallGuard: Could not find TargetDirectory variable");
                }
                else
                {
                    session.Log($"InstallGuard: Install directory is {installDir}");
                }

                foreach (var proc in Process.GetProcesses())
                {
                    string mainModulePath = string.Empty;

                    try
                    {
                        if (proc.Id == Process.GetCurrentProcess().Id)
                        {
                            continue;
                        }

                        mainModulePath = proc.MainModule.FileName;

                        session.Log("InstallGuard: Module Path = '{0}'", mainModulePath);
                    }
                    catch
                    {

                    }

                    if (mainModulePath != null && mainModulePath.Length > 0 && mainModulePath.IndexOf(installDir) != -1)
                    {
                        session.Log($"InstallGuard: Found running CloudVeil instance.");

                        return ActionResult.Failure;
                    }
                }
            }
            catch(Exception ex)
            {
                session.Log("GuardInstall error occurred {0}", ex);
            }

            return ActionResult.Success;
        }
    }
}
