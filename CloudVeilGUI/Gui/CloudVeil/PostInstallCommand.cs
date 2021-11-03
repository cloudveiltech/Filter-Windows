/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Microsoft.Win32;
using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Gui.CloudVeil
{
    public enum ShutdownFlags
    {
        SHUTDOWN_FORCE_OTHERS = 0x1,
        SHUTDOWN_FORCE_SELF = 0x2,
        SHUTDOWN_RESTART = 0x4,
        SHUTDOWN_NOREBOOT = 0x10,
        SHUTDOWN_GRACE_OVERRIDE = 0x20,
        SHUTDOWN_INSTALL_UPDATES = 0x40,
        SHUTDOWN_RESTARTAPPS = 0x80,
        SHUTDOWN_HYBRID = 0x200
    }

    // XXX TODO There are some steps you need to take for the post-install exec to work correctly
    // when making a 64 bit MSI installer. You need to modify the 64 bit MSI as described at the
    // following locations:
    // http://stackoverflow.com/questions/10275106/badimageformatexception-x64-issue/10281533#10281533 http://stackoverflow.com/questions/5475820/system-badimageformatexception-when-installing-program-from-vs2010-installer-pro/6797989#6797989
    //
    // Just in case. Steps are: First, ensure you have Orca installed. Run Orca and open your
    // project's MSI folder Select the Binary table Double click the cell [Binary Data] for the
    // record InstallUtil Make sure "Read binary from filename" is selected Click the Browse button
    // Browse to C:\Windows\Microsoft.NET\Framework64\v4.0.30319 Select InstallUtilLib.dll Click the
    // Open button and then the OK button
    [RunInstaller(true)]
    public partial class PostInstallCommand : System.Configuration.Install.Installer
    {
        /// <summary>
        /// Helper methods for working with <see cref="Guid"/>.
        /// </summary>
        public static class GuidUtility
        {
            /// <summary>
            /// Creates a name-based UUID using the algorithm from RFC 4122 §4.3.
            /// </summary>
            /// <param name="namespaceId">
            /// The ID of the namespace.
            /// </param>
            /// <param name="name">
            /// The name (within that namespace).
            /// </param>
            /// <returns>
            /// A UUID derived from the namespace and name.
            /// </returns>
            /// <remarks>
            /// See <a
            /// href="http://code.logos.com/blog/2011/04/generating_a_deterministic_guid.html">Generating
            /// a deterministic GUID</a>.
            /// </remarks>
            public static Guid Create(Guid namespaceId, string name)
            {
                return Create(namespaceId, name, 5);
            }

            /// <summary>
            /// Creates a name-based UUID using the algorithm from RFC 4122 §4.3.
            /// </summary>
            /// <param name="namespaceId">
            /// The ID of the namespace.
            /// </param>
            /// <param name="name">
            /// The name (within that namespace).
            /// </param>
            /// <param name="version">
            /// The version number of the UUID to create; this value must be either 3 (for MD5 hashing)
            /// or 5 (for SHA-1 hashing).
            /// </param>
            /// <returns>
            /// A UUID derived from the namespace and name.
            /// </returns>
            /// <remarks>
            /// See <a
            /// href="http://code.logos.com/blog/2011/04/generating_a_deterministic_guid.html">Generating
            /// a deterministic GUID</a>.
            /// </remarks>
            public static Guid Create(Guid namespaceId, string name, int version)
            {
                if (name == null)
                    throw new ArgumentNullException("name");
                if (version != 3 && version != 5)
                    throw new ArgumentOutOfRangeException("version", "version must be either 3 or 5.");

                // convert the name to a sequence of octets (as defined by the standard or conventions of
                // its namespace) (step 3)
                // ASSUME: UTF-8 encoding is always appropriate
                byte[] nameBytes = Encoding.UTF8.GetBytes(name);

                // convert the namespace UUID to network order (step 3)
                byte[] namespaceBytes = namespaceId.ToByteArray();
                SwapByteOrder(namespaceBytes);

                // comput the hash of the name space ID concatenated with the name (step 4)
                byte[] hash;
                using (HashAlgorithm algorithm = version == 3 ? (HashAlgorithm)MD5.Create() : SHA1.Create())
                {
                    algorithm.TransformBlock(namespaceBytes, 0, namespaceBytes.Length, null, 0);
                    algorithm.TransformFinalBlock(nameBytes, 0, nameBytes.Length);
                    hash = algorithm.Hash;
                }

                // most bytes from the hash are copied straight to the bytes of the new GUID (steps 5-7,
                // 9, 11-12)
                byte[] newGuid = new byte[16];
                Array.Copy(hash, 0, newGuid, 0, 16);

                // set the four most significant bits (bits 12 through 15) of the time_hi_and_version
                // field to the appropriate 4-bit version number from Section 4.1.3 (step 8)
                newGuid[6] = (byte)((newGuid[6] & 0x0F) | (version << 4));

                // set the two most significant bits (bits 6 and 7) of the clock_seq_hi_and_reserved to
                // zero and one, respectively (step 10)
                newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);

                // convert the resulting UUID to local byte order (step 13)
                SwapByteOrder(newGuid);
                return new Guid(newGuid);
            }

            /// <summary>
            /// The namespace for fully-qualified domain names (from RFC 4122, Appendix C).
            /// </summary>
            public static readonly Guid DnsNamespace = new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

            /// <summary>
            /// The namespace for URLs (from RFC 4122, Appendix C).
            /// </summary>
            public static readonly Guid UrlNamespace = new Guid("6ba7b811-9dad-11d1-80b4-00c04fd430c8");

            /// <summary>
            /// The namespace for ISO OIDs (from RFC 4122, Appendix C).
            /// </summary>
            public static readonly Guid IsoOidNamespace = new Guid("6ba7b812-9dad-11d1-80b4-00c04fd430c8");

            // Converts a GUID (expressed as a byte array) to/from network order (MSB-first).
            internal static void SwapByteOrder(byte[] guid)
            {
                SwapBytes(guid, 0, 3);
                SwapBytes(guid, 1, 2);
                SwapBytes(guid, 4, 5);
                SwapBytes(guid, 6, 7);
            }

            private static void SwapBytes(byte[] guid, int left, int right)
            {
                byte temp = guid[left];
                guid[left] = guid[right];
                guid[right] = temp;
            }
        }

        public class RegistryUtil
        {
            private object m_emailLock = new object();
            private object m_authenticationLock = new object();

            /// <summary>
            /// Abstracts the creation of our app's registry key away from the two properties.
            /// </summary>
            /// <param name="writeable">Should we get writeable permission?</param>
            /// <param name="createKey">Should we create the key if it doesn't exist?</param>
            /// <returns>registry key on success, or null otherwise</returns>
            private RegistryKey getAppRegistryKey(bool writeable = false, bool createKey = false)
            {
                // Get the name of our process, aka the Executable name.
                var applicationNiceName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().ProcessName);


                // Open the LOCAL_MACHINE\SYSTEM sub key for read/write.
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software", writeable))
                {
                    // Create or open our application's key.

                    RegistryKey sub = null;

                    if (!createKey)
                    {
                        sub = key.OpenSubKey(applicationNiceName, writeable);
                    }
                    else
                    {
                        try
                        {
                            sub = key.OpenSubKey(applicationNiceName, writeable);
                        }
                        catch
                        {

                        }

                        if (sub == null)
                        {
                            try
                            {
                                key.DeleteSubKey(applicationNiceName, false);
                                sub = key.CreateSubKey(applicationNiceName);
                            }
                            catch
                            {
                                sub = null;
                            }
                        }
                    }

                    return sub;

                }
            }

            /// <summary>
            /// Stores the email that was granted the auth token.
            /// </summary>
            public string UserEmail
            {
                get
                {
                    lock (m_emailLock)
                    {
                        string machineName = string.Empty;

                        try
                        {
                            machineName = System.Environment.MachineName;
                        }
                        catch
                        {
                            machineName = "Unknown";
                        }

                        // This key will have the entropy written to it in the registry.
                        string keyName = GuidUtility.Create(GuidUtility.UrlNamespace, Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\" + machineName + @"\email-address").ToString();

                        using (var sub = getAppRegistryKey(createKey: false))
                        {
                            string emailAddress = null;

                            if (sub != null)
                            {
                                emailAddress = sub.GetValue(keyName) as string;

                                if (emailAddress == null || emailAddress.Length == 0)
                                {
                                    return null;
                                }
                            }

                            return emailAddress;
                        }
                    }
                }

                set
                {
                    Debug.Assert(value != null && value.Length > 0);

                    lock (m_emailLock)
                    {
                        string machineName = string.Empty;

                        try
                        {
                            machineName = System.Environment.MachineName;
                        }
                        catch
                        {
                            machineName = "Unknown";
                        }

                        // This key will have the entropy written to it in the registry.
                        string keyName = GuidUtility.Create(GuidUtility.UrlNamespace, Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\" + machineName + @"\email-address").ToString();

                        // Get the name of our process, aka the Executable name.
                        var applicationNiceName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().ProcessName);

                        // Open the LOCAL_MACHINE\SYSTEM sub key for read/write.
                        using (RegistryKey sub = getAppRegistryKey(true, true))
                        {
                            // Create or open our application's key.

                            if (sub != null)
                            {
                                try
                                {
                                    sub.SetValue(keyName, value, RegistryValueKind.String);
                                }
                                catch (Exception e)
                                {
                                    System.Diagnostics.Debug.WriteLine("sub.SetValue threw exception {0}", e.ToString());
                                }
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Holds the auth token returned from the last successful auth request. 
            /// </summary>
            public string AuthToken
            {
                get
                {
                    lock (m_authenticationLock)
                    {
                        string machineName = string.Empty;

                        try
                        {
                            machineName = System.Environment.MachineName;
                        }
                        catch
                        {
                            machineName = "Unknown";
                        }

                        // This key will have the entropy written to it in the registry.
                        string keyName = GuidUtility.Create(GuidUtility.UrlNamespace, Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\" + machineName).ToString();

                        // Get the name of our process, aka the Executable name.
                        var applicationNiceName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().ProcessName);

                        // Open the LOCAL_MACHINE\SYSTEM sub key for read/write.
                        using (RegistryKey sub = getAppRegistryKey())
                        {
                            // Create or open our application's key.

                            string authToken = null;

                            if (sub != null)
                            {
                                authToken = sub.GetValue(keyName) as string;

                                if (authToken == null || authToken.Length == 0)
                                {
                                    return null;
                                }
                            }

                            return authToken;
                        }
                    }
                }

                set
                {
                    Debug.Assert(value != null && value.Length > 0);

                    lock (m_authenticationLock)
                    {
                        string machineName = string.Empty;

                        try
                        {
                            machineName = System.Environment.MachineName;
                        }
                        catch
                        {
                            machineName = "Unknown";
                        }

                        // This key will have the entropy written to it in the registry.
                        string keyName = GuidUtility.Create(GuidUtility.UrlNamespace, Environment.GetFolderPath(Environment.SpecialFolder.Windows) + @"\" + machineName).ToString();

                        // Get the name of our process, aka the Executable name.
                        var applicationNiceName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().ProcessName);

                        // Open the LOCAL_MACHINE\SYSTEM sub key for read/write.
                        using (RegistryKey sub = getAppRegistryKey(true, true))
                        {
                            if (sub != null)
                            {
                                try
                                {
                                    sub.SetValue(keyName, value, RegistryValueKind.String);
                                }
                                catch (Exception e)
                                {
                                    System.Diagnostics.Debug.WriteLine("sub.SetValue threw exception {0}", e.ToString());
                                }
                            }
                        }
                    }
                }
            }
        }

        public PostInstallCommand()
        {
            InitializeComponent();
        }

        public override void Uninstall(IDictionary savedState)
        {
            base.Uninstall(savedState);

            // Purge registration token.
            //deleteRegistryKey();
        }

        public override void Install(IDictionary stateSaver)
        {
            base.Install(stateSaver);

            /*string[] interferingProcessNames =
            new string[] {

            };

            Process[] processes = Process.GetProcesses();
            foreach(var process in  processes)
            {
                
            }

            RegistryUtil registry = new RegistryUtil();

            string authTokenPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "authtoken.data");
            string emailPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "email.data");

            try
            {
                if(File.Exists(authTokenPath))
                {
                    File.Delete(authTokenPath);
                }

                using (StreamWriter writer = File.CreateText(authTokenPath))
                {
                    writer.Write(registry.AuthToken);
                }
            }
            catch
            {

            }

            try
            {
                if(File.Exists(emailPath))
                {
                    File.Delete(emailPath);
                }
                
                using (StreamWriter writer = File.CreateText(emailPath))
                {
                    writer.Write(registry.UserEmail);
                }
            }
            catch
            {

            }*/
        }

        public override void Commit(IDictionary savedState)
        {   
            base.Commit(savedState);
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            System.Diagnostics.Process.Start(Assembly.GetExecutingAssembly().Location);

            var filterServiceAssemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "FilterServiceProvider.exe");

            var uninstallStartInfo = new ProcessStartInfo(filterServiceAssemblyPath);
            uninstallStartInfo.Arguments = "Uninstall";
            uninstallStartInfo.UseShellExecute = false;
            uninstallStartInfo.CreateNoWindow = true;
            var uninstallProc = Process.Start(uninstallStartInfo);
            uninstallProc.WaitForExit();

            var installStartInfo = new ProcessStartInfo(filterServiceAssemblyPath);
            installStartInfo.Arguments = "Install";
            installStartInfo.UseShellExecute = false;
            installStartInfo.CreateNoWindow = true;

            var installProc = Process.Start(installStartInfo);
            installProc.WaitForExit();

            var imageServiceAssemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ImageFilter\\ImageFilter.exe");

            uninstallStartInfo = new ProcessStartInfo(imageServiceAssemblyPath);
            uninstallStartInfo.Arguments = "Uninstall";
            uninstallStartInfo.UseShellExecute = false;
            uninstallStartInfo.CreateNoWindow = true;
            uninstallProc = Process.Start(uninstallStartInfo);
            uninstallProc.WaitForExit();

            installStartInfo = new ProcessStartInfo(imageServiceAssemblyPath);
            installStartInfo.Arguments = "Install";
            installStartInfo.UseShellExecute = false;
            installStartInfo.CreateNoWindow = true;

            installProc = Process.Start(installStartInfo);
            installProc.WaitForExit();

            string restartFlagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CloudVeil", "restart.flag");

            // 'norestart' not defined in Context.Parameters, so we can't use Context.IsParameterTrue.
            // This file gets defined by the filter service before shutting down.
            if (File.Exists(restartFlagPath))
            {
                File.Delete(restartFlagPath);
                InitiateShutdown(null, null, 0, (uint)(ShutdownFlags.SHUTDOWN_FORCE_OTHERS | ShutdownFlags.SHUTDOWN_RESTART | ShutdownFlags.SHUTDOWN_RESTARTAPPS), 0);
            }

            RegistryUtil registry = new RegistryUtil();

            if (savedState.Contains("__my_registry_auth_token"))
            {
                registry.AuthToken = savedState["__my_registry_auth_token"] as string;
            }

            if(savedState.Contains("__my_registry_user_email"))
            {
                registry.UserEmail = savedState["__my_registry_user_email"] as string;
            }

            EnsureStartServicePostInstall(filterServiceAssemblyPath);
            EnsureStartServicePostInstall(imageServiceAssemblyPath);

            Environment.Exit(0);

            base.Dispose();
        }

        private void EnsureStartServicePostInstall(string filterServiceAssemblyPath)
        {
            // XXX TODO - This is a dirty hack.
            int tries = 0;

            while(!TryStartService(filterServiceAssemblyPath) && tries < 20)
            {
                Task.Delay(200).Wait();
                ++tries;
            }
        }

        private bool TryStartService(string filterServiceAssemblyPath)
        {
            try
            {
                TimeSpan timeout = TimeSpan.FromSeconds(60);

                foreach(var service in ServiceController.GetServices())
                {
                    if(service.ServiceName.IndexOf(Path.GetFileNameWithoutExtension(filterServiceAssemblyPath), StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        if(service.Status == ServiceControllerStatus.StartPending)
                        {
                            service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                        }

                        if(service.Status != ServiceControllerStatus.Running)
                        {
                            service.Start();
                            service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void deleteRegistryKey()
        {
            var applicationNiceName = "FilterServiceProvider";

            // Open the LOCAL_MACHINE\Software sub key for read/write.
            using (RegistryKey softwareKey = Registry.LocalMachine.OpenSubKey("Software", true))
            {
                try
                {
                    softwareKey.DeleteSubKeyTree(applicationNiceName);
                }
                catch(Exception ex)
                {
                    throw new InstallException("Could not delete FilterServiceProvider key because of " + ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern UInt32 InitiateShutdown(
            string lpMachineName,
            string lpMessage,
            UInt32 dwGracePeriod,
            UInt32 dwShutdownFlags,
            UInt32 dwReason);
    }
}