/*
* Copyright � 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using Filter.Platform.Common;
using Filter.Platform.Common.Util;
using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace CloudVeil.Core.Windows.Util
{
    public class WindowsFingerprint : IFingerprint
    {   
        static WindowsFingerprint()
        {
            using(var sec = new SHA1CryptoServiceProvider())
            {
                var sb = new StringBuilder();
                var sbShort = new StringBuilder();

                ManagementObjectCollection collection = null;
                ManagementObjectSearcher searcher = null;

                searcher = new ManagementObjectSearcher("Select * From Win32_BIOS");
                collection = searcher.Get();
                foreach(ManagementObject mo in collection)
                {
                    try
                    {
                        sb.Append(mo["SerialNumber"].ToString());
                        sbShort.Append(mo["SerialNumber"].ToString());
                    }
                    catch { }

                    try
                    {
                        sb.Append(mo["Manufacturer"].ToString());
                        sbShort.Append(mo["Manufacturer"].ToString());
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
                        sbShort.Append(mo["SerialNumber"].ToString());
                    }
                    catch { }

                    try
                    {
                        sb.Append(mo["Manufacturer"].ToString());
                        sbShort.Append(mo["Manufacturer"].ToString());
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

                LoggerUtil.GetAppWideLogger()?.Info("Fingerprint seed is " + sb.ToString());
                LoggerUtil.GetAppWideLogger()?.Info("Fingerprint short seed is " + sbShort.ToString());
                byte[] bt = sec.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                s_fingerPrintLong = BitConverter.ToString(bt).Replace("-", "");
                
                bt = sec.ComputeHash(Encoding.UTF8.GetBytes(sbShort.ToString()));
                s_fingerPrintShort = BitConverter.ToString(bt).Replace("-", "");

                LoggerUtil.GetAppWideLogger()?.Info("My fingerprint is {0} and {1}", s_fingerPrintLong, s_fingerPrintShort);
            }
        }

        /// <summary>
        /// Container for the device unique ID.
        /// </summary>
        private static string s_fingerPrintLong;
        private static string s_fingerPrintShort;

        /// <summary>
        /// Gets a unique identifier for this device based on miscelleneous unique ID's.
        /// </summary>
        public string Value
        {
            get
            {
                return s_fingerPrintLong;
            }
        }

        public string Value2
        {
            get
            {
                return s_fingerPrintShort;
            }
        }
    }
}