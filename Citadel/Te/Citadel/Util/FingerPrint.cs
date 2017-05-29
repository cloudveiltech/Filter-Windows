/*
 * Copyright © 2017 Jesse Nicholson  
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace Te.Citadel.Util
{
    internal class FingerPrint
    {   
        static FingerPrint()
        {
            using(var sec = new SHA1CryptoServiceProvider())
            {
                var sb = new StringBuilder();

                ManagementObjectCollection collection = null;
                ManagementObjectSearcher searcher = null;

                searcher = new ManagementObjectSearcher("Select * From Win32_BIOS");
                collection = searcher.Get();
                foreach(ManagementObject mo in collection)
                {
                    try
                    {
                        sb.Append(mo["SerialNumber"].ToString());
                    }
                    catch { }

                    try
                    {
                        sb.Append(mo["Manufacturer"].ToString());
                    }
                    catch { }

                    try
                    {
                        sb.Append(mo["Name"].ToString());
                    }
                    catch { }
                }
                collection.Dispose();
                searcher.Dispose();

                searcher = new ManagementObjectSearcher("Select * From Win32_BaseBoard");
                collection = searcher.Get();
                foreach(ManagementObject mo in collection)
                {
                    try
                    {
                        sb.Append(mo["SerialNumber"].ToString());
                    }
                    catch { }

                    try
                    {
                        sb.Append(mo["Manufacturer"].ToString());
                    }
                    catch { }

                    try
                    {
                        sb.Append(mo["Name"].ToString());
                    }
                    catch { }
                }
                collection.Dispose();
                searcher.Dispose();

                byte[] bt = sec.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                s_fingerPrint = BitConverter.ToString(bt).Replace("-", "");
            }
        }

        /// <summary>
        /// Container for the device unique ID.
        /// </summary>
        private static string s_fingerPrint;

        /// <summary>
        /// Gets a unique identifier for this device based on miscelleneous unique ID's.
        /// </summary>
        public static string Value
        {
            get
            {
                return s_fingerPrint;
            }
        }
    }
}