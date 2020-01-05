using Microsoft.Deployment.WindowsInstaller;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

            foreach(ServiceController sc in services)
            {
                if(sc.ServiceName == "WinDivert")
                {
                    session.Log("WinDivert service found.");

                    try
                    {
                        sc.Stop();
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
