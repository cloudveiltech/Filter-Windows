using Microsoft.Deployment.WindowsInstaller;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InstallerCustomActions
{
    public class UpdateUIConfiguration
    {
        [CustomAction]
        public static ActionResult ConfigureUI(Session session)
        {
            try
            {
                string updateStr = session["UPDATECV4W"];

                session.Log($"UPDATECV4W is {updateStr}");

                Installer.SetExternalUI(HandleUIMessages, InstallLogModes.Error);
                if (updateStr == "true") Installer.SetInternalUI(InstallUIOptions.Reduced);

                
            }
            catch
            {
                session.Log("UPDATECV4W is not defined.");
            }

            return ActionResult.Success;
        }

        private static MessageResult HandleUIMessages(InstallMessage messageType, string message, MessageButtons buttons, MessageIcon icon, MessageDefaultButton defaultButton)
        {
            File.AppendAllText(@"C:\Users\Kent\log2.log", $"{messageType}: {message}, {(int)buttons}, {(int)icon}, {(int)defaultButton}");
            return MessageResult.None;
        }
    }
}
