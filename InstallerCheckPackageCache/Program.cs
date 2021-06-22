using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace InstallerCheckPackageCache
{
    class Program
    {
        static void Main(string[] args)
        {
            CheckAndRestoreCache();
        }


        private static void CheckAndRestoreCache()
        {
            List<Cv4wInstaledVersion> list = GetInstalledCv4WVersions();

            foreach (var installedProduct in list)
            {
                var versionParts = installedProduct.Version.Split(new char[] { '.' });
                if(versionParts.Length < 3)
                {
                    continue;
                }
                var version = versionParts[0] + "." + versionParts[1] + "." + versionParts[2];
                var cacheDir = GetCacheDir(installedProduct.PackageId, version);
                var cacheFile = cacheDir + @"\CloudVeil.msi";
                if (!Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }
                if (!File.Exists(cacheFile))
                {
                    DownloadPackageCache(cacheFile, version);
                }
            }
        }

        private static void DownloadPackageCache(string outputPath, string version)
        {
            WebClient client = new WebClient();
            var platform = Environment.Is64BitOperatingSystem ? "winx64" : "winx86";
            var url = CloudVeil.CompileSecrets.ServiceProviderApiPath + "/releases/" + "CloudVeil-" + version + "-" + platform + ".msi";
            try
            {
                client.DownloadFile(url, outputPath);
            }
            catch (Exception)
            {
                
            }
        }

        private static string GetCacheDir(string productId, string version)
        {
            var cacheDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            cacheDir = cacheDir + @"\Package Cache\" + productId + "v" + version;
            return cacheDir;
        }

        public struct Cv4wInstaledVersion
        {
            public Cv4wInstaledVersion(string packageId, string version) : this()
            {
                this.PackageId = packageId;
                Version = version;
            }

            public string PackageId { get; set; }
            public string Version { get; set; }
        }

        private static List<Cv4wInstaledVersion> GetInstalledCv4WVersions()
        {
            var result = new List<Cv4wInstaledVersion>();
            RegistryKey localKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
            using (RegistryKey key = localKey.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", false))
            {
                foreach(var subkeyName in key.GetSubKeyNames())
                {
                    using(RegistryKey subKey = key.OpenSubKey(subkeyName))
                    {
                        string version = (string)subKey.GetValue("DisplayVersion");
                        string name = (string)subKey.GetValue("DisplayName");
                        if (name == "CloudVeil For Windows") 
                        {
                            result.Add(new Cv4wInstaledVersion(subkeyName, version));
                        }
                    }
                }
            }

            return result;
        }
    }
}
