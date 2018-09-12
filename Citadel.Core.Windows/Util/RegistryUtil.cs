/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Te.Citadel.Util;

namespace Citadel.Core.Windows.Util
{
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
}
