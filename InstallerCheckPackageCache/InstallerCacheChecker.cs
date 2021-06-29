using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace InstallerCheckPackageCache
{
    class InstallerCacheChecker
    {
        enum FileType
        {
            MSI, EXE
        }

        public void CheckAndRestoreCache()
        {
            List<Cv4wInstaledVersion> list = GetInstalledCv4WVersions();

            foreach (var installedProduct in list)
            {
                var versionParts = installedProduct.Version.Split(new char[] { '.' });
                if (versionParts.Length < 3)
                {
                    continue;
                }
                var version = versionParts[0] + "." + versionParts[1] + "." + versionParts[2];
                var cacheDir = GetCacheDir(installedProduct.MsiPackageId, version);
                var cacheFile = cacheDir + @"\CloudVeil.msi";
                if (!Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }
                if (!File.Exists(cacheFile))
                {
                    DownloadPackageCache(cacheFile, version, FileType.MSI);
                }

                cacheDir = GetCacheDir(installedProduct.ExePackageId);

                var platform = Environment.Is64BitOperatingSystem ? "x64" : "x86";
                cacheFile = cacheDir + @"\CloudVeilInstaller-" + platform + ".exe";
                if (!Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }
                if (!File.Exists(cacheFile))
                {
                    DownloadPackageCache(cacheFile, version, FileType.EXE);
                }
            }
        }

        private void DownloadPackageCache(string outputPath, string version, FileType type)
        {
            WebClient client = new WebClient();
            var url = GetRemoteFileUrl(type, version);
            try
            {
                Console.WriteLine($"Downloading from {url}");
                client.DownloadFile(url, outputPath);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception " + e.ToString());
            }
        }


        private string GetRemoteFileUrl(FileType type, string version)
        {
            var platform = Environment.Is64BitOperatingSystem ? "x64" : "x86";
            if (type == FileType.MSI)
            {
                return CloudVeil.CompileSecrets.ServiceProviderApiPath + "/releases/" + "CloudVeil-" + version + "-win" + platform + ".msi";
            }
            else
            {
                return CloudVeil.CompileSecrets.ServiceProviderApiPath + "/releases/" + "CloudVeilInstaller-" + version + "-cv4w-" + platform + ".exe";
            }
        }

        private string GetCacheDir(string productId, string version)
        {
            return GetCacheDir(productId) + "v" + version;
        }

        private string GetCacheDir(string productId)
        {
            var cacheDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            cacheDir = cacheDir + @"\Package Cache\" + productId;
            return cacheDir;
        }

        public struct Cv4wInstaledVersion
        {
            public Cv4wInstaledVersion(string msiPackageId, string exePackageId, string version) : this()
            {
                MsiPackageId = msiPackageId;
                ExePackageId = exePackageId;
                Version = version;
            }

            public string MsiPackageId { get; set; }
            public string ExePackageId { get; set; }
            public string Version { get; set; }
        }

        private List<Cv4wInstaledVersion> GetInstalledCv4WVersions()
        {
            var result = new List<Cv4wInstaledVersion>();
            RegistryKey localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
            using (RegistryKey key = localKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", false))
            {
                if (key == null)
                {
                    return result;
                }
                foreach (var msiPackageIdKey in key.GetSubKeyNames())
                {
                    using (RegistryKey subKey = key.OpenSubKey(msiPackageIdKey))
                    {
                        if (subKey == null)
                        {
                            continue;
                        }
                        string version = (string)subKey.GetValue("DisplayVersion");
                        string name = (string)subKey.GetValue("DisplayName");
                        if (name == "CloudVeil For Windows")
                        {
                            string exePackageId = FindExePackageId(msiPackageIdKey);
                            result.Add(new Cv4wInstaledVersion(msiPackageIdKey, exePackageId, version));
                        }
                    }
                }
            }

            return result;
        }

        private string FindExePackageId(string msiPackageId)
        {
            RegistryKey localKey = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
            using (RegistryKey key = localKey.OpenSubKey(@"Installer\Dependencies\" + msiPackageId + @"\Dependents\", false))
            {
                if (key != null)
                {
                    foreach (var subkeyName in key.GetSubKeyNames())
                    {
                        return subkeyName;
                    }
                }
            }
            return "";
        }
    }
}
