using Microsoft.Deployment.WindowsInstaller;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace InstallerCustomActions
{
    public class CloudVeilGuiStopper
    {
        [CustomAction]
        public static ActionResult StopCloudVeilGui(Session session)
        {
            try
            {
                string installDir = session.CustomActionData["TargetDirectory"];

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

                        session.Log("StopCloudVeilGui: Module Path = '{0}'", mainModulePath);
                    }
                    catch
                    {

                    }

                    if (mainModulePath != null && mainModulePath.Length > 0 && mainModulePath.IndexOf(installDir) != -1)
                    {
                        session.Log($"StopCloudVeilGui: Found running CloudVeil instance. Stopping.");
                        proc.Kill();
                        return ActionResult.Success;
                    }
                }
            }
            catch (Exception ex)
            {
                session.Log("StopCloudVeilGui error occurred {0}", ex);
            }

            return ActionResult.Success;
        }
    }
}
