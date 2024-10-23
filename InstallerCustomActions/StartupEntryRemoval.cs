using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace InstallerCustomActions
{
    public class StartupEntryRemoval
    {
        private static ActionResult deleteStartupEntry(Session session, RegistryKey rootKey, string entryName)
        {
            try
            {
                using (RegistryKey key = rootKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return ActionResult.NotExecuted;

                    object valueObj = key.GetValue(entryName);

                    if (valueObj != null)
                    {
                        key.DeleteValue(entryName);
                        key.Close();
                    }
                    else
                    {
                        session.Log($"Value {entryName} was not defined in registry key.");
                    }
                }

                return ActionResult.Success;
            }
            catch (Exception ex)
            {
                session.Log($"Failed to delete startup entry. {ex}");
                return ActionResult.NotExecuted;
            }
        }

        [CustomAction]
        public static ActionResult RemoveCloudVeilStartupEntry(Session session)
        {
            string[] userKeys = Registry.Users.GetSubKeyNames();

            ActionResult finalResult = ActionResult.NotExecuted;

            foreach (string userKeyName in userKeys)
            {
                using (RegistryKey userKey = Registry.Users.OpenSubKey(userKeyName, true))
                {
                    ActionResult res = deleteStartupEntry(session, userKey, "CloudVeil");
                    
                    if(res != ActionResult.NotExecuted)
                    {
                        finalResult = res;
                    }

                    userKey.Close();
                }
            }
            if (finalResult == ActionResult.Success)
            {
                session.Log($"RemoveCloudVeilStartupEntry {finalResult}");
                return ActionResult.Success;
            }
            else
            {
                return ActionResult.NotExecuted;
            }
        }
    }
}
