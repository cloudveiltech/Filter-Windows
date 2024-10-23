using Microsoft.Deployment.WindowsInstaller;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace InstallerCustomActions
{
    public class RemoveUninstallEntry
    {
        const string BUNDLE_UPGRADE_CODE = "{F034362E-0800-43D6-BE30-721747F8A948}"; //shoud not be changed for the lifetime

        private static void removeUninstallEntry(RegistryKey localMachineKey, Session session)
        {
            List<Tuple<string, Version>> versionsFound = new List<Tuple<string, Version>>();
            Version maxVersion = null;
            localMachineKey.GetSubKeyNames().ToList().ForEach(key =>
            {
                var uninstallKey = localMachineKey.OpenSubKey(key, true);
                if (uninstallKey != null)
                {
                    var upgradeCodes = uninstallKey.GetValue("BundleUpgradeCode");
                    if (upgradeCodes != null)
                    {
                        var stringList = new List<string>(upgradeCodes as string[]);
                        foreach (var str in stringList)
                        {
                            if (str == BUNDLE_UPGRADE_CODE)
                            {
                                var version = uninstallKey.GetValue("BundleVersion");
                                if (version != null)
                                {
                                    var v = new Version(version.ToString());
                                    if (maxVersion == null || maxVersion < v)
                                    {
                                        maxVersion = v;
                                    }
                                    versionsFound.Add(new Tuple<string, Version>(key, v));
                                    session.Log("Found entry of version " + v.ToString());
                                }
                            }
                        }
                      
                    }
                }
            });

            versionsFound.ForEach(tuple => {
                if (tuple.Item2 != maxVersion)
                {
                    using (var key = localMachineKey.OpenSubKey(tuple.Item1, true))
                    {
                        var cachePath = key.GetValue("BundleCachePath");
                        if (cachePath != null)
                        {
                            var dirPath = new FileInfo(cachePath.ToString()).Directory.FullName;
                            Directory.Delete(dirPath, true); 
                            session.Log("Deleted cache of entry " + cachePath);
                        }
                        localMachineKey.DeleteSubKeyTree(tuple.Item1);
                    }
                }
            });

        }
        [CustomAction]
        public static ActionResult RemoveCloudVeilUninstallEntry(Session session)
        {
            ActionResult finalResult = ActionResult.Success;

            using (RegistryKey localMachineKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall", true))
            {
                removeUninstallEntry(localMachineKey, session);
            }

            using (RegistryKey localMachineKey = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall", true))
            {
                removeUninstallEntry(localMachineKey, session);
            }

            if (finalResult == ActionResult.Success)
            {
                session.Log($"RemoveCloudVeilUninstallEntry {finalResult}");
                return ActionResult.Success;
            }
            else
            {
                return ActionResult.NotExecuted;
            }
        }
    }
}
