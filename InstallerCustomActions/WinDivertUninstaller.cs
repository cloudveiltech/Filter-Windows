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
    public class WinDivertUninstaller
    {
        [CustomAction]
        public static ActionResult RemoveWinDivert(Session session)
        {
            ServiceController[] services = null;

            try
            {
                services = ServiceController.GetDevices();
            }
            catch (Win32Exception ex)
            {
                session.Log($"Failed to get list of services. {ex}");
                return ActionResult.NotExecuted;
            }

            foreach(ServiceController serviceController in services)
            {
                if(serviceController.ServiceName == "WinDivert")
                {
                    session.Log("WinDivert service found.");

                    try
                    {
                        if (serviceController.Status != ServiceControllerStatus.Stopped && serviceController.Status != ServiceControllerStatus.StopPending)
                        {
                            serviceController.Stop();
                            serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        }

                        ProcessStartInfo psi = new ProcessStartInfo("sc.exe")
                        {
                            Arguments = string.Format("delete \"{0}\"", serviceController.ServiceName),
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using (Process process = Process.Start(psi))
                        {
                            process.WaitForExit();
                            if (process.ExitCode != 0)
                            {
                                // Handle error or log output
                                string output = process.StandardOutput.ReadToEnd();
                                session.Log($"Failed to stop WinDivert. Exit Code: {process.ExitCode}. Output: {output}");                                
                            }                            
                        }
                    }
                    catch(Exception ex)
                    {
                        session.Log($"Failed to stop WinDivert with {ex}");
                        return ActionResult.NotExecuted;
                    }

                    break;
                }
            }

            return ActionResult.Success;
        }
    }
}
