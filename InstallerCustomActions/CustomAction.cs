using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Deployment.WindowsInstaller;

namespace InstallerCustomActions
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult DetectInstalledPrograms(Session session)
        {

        }
    }
}
