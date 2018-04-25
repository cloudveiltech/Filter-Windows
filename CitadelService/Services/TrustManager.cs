using Citadel.Core.Extensions;
using Citadel.Core.Windows.Util;
using murrayju.ProcessExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace CitadelService.Services
{
    public class TrustManager
    {
        private NLog.Logger m_logger;

        public TrustManager()
        {
            m_logger = LoggerUtil.GetAppWideLogger();
        }

        /// <summary>
        /// Searches for git installations and adds CitadelCore certificate to the CA certificate bundle.
        /// </summary>
        public void EstablishTrustWithGit()
        {
            // 1. We search in three places for git installation.
            // b. %localappdata%\Atlassian\Sourcetree\git_local
            // c. %ProgramFiles%\Git
            // d. %ProgramFiles(x86)%\Git

            string atlassianGitPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Atlassian", "Sourcetree", "git_local");
            string programFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git");
            string programFilesX86Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git");

            List<string> directoryPaths = new List<string>();
            directoryPaths.Add(atlassianGitPath);
            directoryPaths.Add(programFilesPath);
            directoryPaths.Add(programFilesX86Path);

            string[] bundlePaths = {
                Path.Combine("bin", "curl-ca-bundle.crt"),
                Path.Combine("mingw32", "ssl", "certs", "ca-bundle.crt"),
                Path.Combine("mingw64", "ssl", "certs", "ca-bundle.crt"),
                Path.Combine("usr", "ssl", "certs", "ca-bundle.crt")
            };

            List<string> installedGits = new List<string>();
            string foundBundlePath = null;
            // 2. Search in the found installation location for usr/ssl/certs/...
            foreach (var directory in directoryPaths)
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                // Search all possible locations for git's CA bundle.
                foreach(var bundlePath in bundlePaths)
                {
                    string path = Path.Combine(directory, bundlePath);

                    if(File.Exists(path))
                    {
                        foundBundlePath = path;
                        break;
                    }
                }

                // Search for bin\git.exe
                string gitPath = Path.Combine(directory, "bin", "git.exe");

                if(File.Exists(gitPath))
                {
                    installedGits.Add(gitPath);
                    m_logger.Info("Found git.exe at {0}", gitPath);
                }
            }

            string cloudVeilCertificateBundlePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "git-certificate-bundle.crt");

            X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection certs = store.Certificates;

            StringBuilder bundleBuilder = new StringBuilder();

            if (foundBundlePath != null)
            {
                string bundleContents = File.ReadAllText(foundBundlePath);
                bundleBuilder = new StringBuilder();
                bundleBuilder.Append(bundleContents);

                if(bundleContents[bundleContents.Length - 1] != '\n')
                {
                    bundleBuilder.Append("\r\n");
                }

                // Look for CN=Citadel Core root CA
                foreach(var cert in certs)
                {
                    if(cert.SubjectName.Decode(X500DistinguishedNameFlags.None) ==  "CN=Citadel Core")
                    {
                        m_logger.Info("Found Citadel Core certificate, adding to bundle.");
                        bundleBuilder.Append(cert.ExportToPem());
                        bundleBuilder.Append("\r\n");
                    }
                }

                File.WriteAllText(cloudVeilCertificateBundlePath, bundleBuilder.ToString());
            }
            else
            {
                foreach(var cert in certs)
                {
                    m_logger.Info("OS Cert {0}", cert.SubjectName.Decode(X500DistinguishedNameFlags.None));
                    bundleBuilder.Append(cert.ExportToPem());
                    bundleBuilder.Append("\r\n");
                }

                File.WriteAllText(cloudVeilCertificateBundlePath, bundleBuilder.ToString());
            }

            // Git apparently requires forward slashes instead of backslashes for its sslCAInfo.
            string gitCompatibleBundlePath = cloudVeilCertificateBundlePath.Replace(Path.DirectorySeparatorChar, '/');

            List<Process> waitFor = new List<Process>();
            // Next, loop through installed git.exe's and set their CA bundle to use the one we just created.
            foreach(string gitExe in installedGits)
            {
                string arguments = $"config --global http.sslCAInfo \"{gitCompatibleBundlePath}\"";
                //ProcessExtensions.StartProcessAsCurrentUser(gitExe, arguments, Path.GetDirectoryName(gitExe), false);
                Process process = Process.Start(new ProcessStartInfo(gitExe, arguments)
                {

                });

                process.WaitForExit(1000);
                m_logger.Info("Git ran with {0}", process.ExitCode);
            }
        }

        /// <summary>
        /// Searches for FireFox installations and enables trust of the local certificate store. 
        /// </summary>
        /// <remarks>
        /// If any profile is discovered that does not have the local CA cert store checking enabled
        /// already, all instances of firefox will be killed and then restarted when calling this method.
        /// </remarks>
        public void EstablishTrustWithFirefox()
        {
            // This path will be DRIVE:\USER_PATH\Public\Desktop
            var usersBasePath = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
            usersBasePath = usersBasePath.Parent;
            usersBasePath = usersBasePath.Parent;

            var ffProfileDirs = new List<string>();

            var userDirs = Directory.GetDirectories(usersBasePath.FullName);

            foreach (var userDir in userDirs)
            {
                if (Directory.Exists(Path.Combine(userDir, @"AppData\Roaming\Mozilla\Firefox\Profiles")))
                {
                    ffProfileDirs.Add(Path.Combine(userDir, @"AppData\Roaming\Mozilla\Firefox\Profiles"));
                }
            }

            if (ffProfileDirs.Count <= 0)
            {
                return;
            }

            var valuesThatNeedToBeSet = new Dictionary<string, string>();

            var firefoxUserCfgValuesUri = "CitadelService.Resources.FireFoxUserCFG.txt";
            using (var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(firefoxUserCfgValuesUri))
            {
                if (resourceStream != null && resourceStream.CanRead)
                {
                    using (TextReader tsr = new StreamReader(resourceStream))
                    {
                        string cfgLine = null;
                        while ((cfgLine = tsr.ReadLine()) != null)
                        {
                            if (cfgLine.Length > 0)
                            {
                                var firstSpace = cfgLine.IndexOf(' ');

                                if (firstSpace != -1)
                                {
                                    var key = cfgLine.Substring(0, firstSpace);
                                    var value = cfgLine.Substring(firstSpace);

                                    if (!valuesThatNeedToBeSet.ContainsKey(key))
                                    {
                                        valuesThatNeedToBeSet.Add(key, value);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    m_logger.Error("Cannot read from firefox cfg resource file.");
                }
            }

            foreach (var ffProfDir in ffProfileDirs)
            {
                var prefsFiles = Directory.GetFiles(ffProfDir, "prefs.js", SearchOption.AllDirectories);

                foreach (var prefFile in prefsFiles)
                {
                    var userFile = Path.Combine(Path.GetDirectoryName(prefFile), "user.js");

                    string[] fileText = new string[0];

                    if (File.Exists(userFile))
                    {
                        fileText = File.ReadAllLines(prefFile);
                    }

                    var notFound = new Dictionary<string, string>();

                    foreach (var kvp in valuesThatNeedToBeSet)
                    {
                        var entryIndex = Array.FindIndex(fileText, l => l.StartsWith(kvp.Key));

                        if (entryIndex != -1)
                        {
                            if (!fileText[entryIndex].EndsWith(kvp.Value))
                            {
                                fileText[entryIndex] = kvp.Key + kvp.Value;
                                m_logger.Info("Firefox profile {0} has has preference {1}) adjusted to be set correctly already.", Directory.GetParent(prefFile).Name, kvp.Key);
                            }
                            else
                            {
                                m_logger.Info("Firefox profile {0} has preference {1}) set correctly already.", Directory.GetParent(prefFile).Name, kvp.Key);
                            }
                        }
                        else
                        {
                            notFound.Add(kvp.Key, kvp.Value);
                        }
                    }

                    var fileTextList = new List<string>(fileText);

                    foreach (var nfk in notFound)
                    {
                        m_logger.Info("Firefox profile {0} is having preference {1}) added.", Directory.GetParent(prefFile).Name, nfk.Key);
                        fileTextList.Add(nfk.Key + nfk.Value);
                    }

                    File.WriteAllLines(userFile, fileTextList);
                }
            }

            // Figure out if firefox is running. If later it is and we kill it, store the path to
            // firefox.exe so we can restart the process after we're done.
            string firefoxExePath = string.Empty;
            Process[] processes = Process.GetProcessesByName("firefox");

            bool firefoxIsRunning = false;
            foreach (var process in processes)
            {
                if (!process.HasExited)
                {
                    firefoxIsRunning = true;
                }
            }

            // Always kill firefox. Firefox may not be open, but we want to make sure it's killed anyway.
            if (processes.Length > 0)
            {
                // We need to kill firefox before editing the preferences, otherwise they'll just get overwritten.
                foreach (var ff in Process.GetProcessesByName("firefox"))
                {
                    firefoxExePath = ff.MainModule.FileName;

                    try
                    {
                        ff.Kill();
                        ff.Dispose();
                    }
                    catch { }
                }
            }

            // Means we force closed at least once instance of firefox. Relaunch it now to cause it
            // to run restore.
            if (firefoxIsRunning && StringExtensions.Valid(firefoxExePath))
            {
                // Start the process and abandon our handle.
                ProcessExtensions.StartProcessAsCurrentUser(firefoxExePath);
            }
        }
    }
}
