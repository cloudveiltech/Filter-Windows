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
using static Topshelf.Runtime.Windows.NativeMethods;

namespace CloudVeil.Core.Windows.Util
{
    public class WindowsFingerprint : IFingerprint
    {
        const string DEFAULT_STRING = "Default string";
        const string NOT_APPLICABLE_STRING = "Not Applicable";
        const string EMPTY_UUID_STRING = "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF";
        static WindowsFingerprint()
        {
            using(var sec = new SHA1CryptoServiceProvider())
            {
                var sb = new StringBuilder();
                var sbShort = new StringBuilder();

                ManagementObjectCollection collection = null;
                ManagementObjectSearcher searcher = null;

                var emptySerialNumber = false;
                searcher = new ManagementObjectSearcher("Select * From Win32_BIOS");
                collection = searcher.Get();
                foreach(ManagementObject mo in collection)
                {
                    try
                    {
                        var serialNumber = mo["SerialNumber"].ToString();
                        if(serialNumber == NOT_APPLICABLE_STRING || serialNumber == DEFAULT_STRING)
                        {
                            serialNumber = string.Empty;
                            emptySerialNumber = true;
                        }

                        sb.Append(serialNumber);
                        sbShort.Append(serialNumber);
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
                        var serialNumber = mo["SerialNumber"].ToString();
                        if (serialNumber == NOT_APPLICABLE_STRING || serialNumber == DEFAULT_STRING)
                        {
                            serialNumber = string.Empty;
                            emptySerialNumber = true;
                        }
                        sb.Append(serialNumber);
                        sbShort.Append(serialNumber);
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

                if(emptySerialNumber)
                {//fallback to uuid
                    LoggerUtil.GetAppWideLogger()?.Info("Fingerprint Fallback to UUID");
                    var emptyUuid = true;
                    searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var uuid = obj["UUID"].ToString();
                        if(uuid !=  NOT_APPLICABLE_STRING && uuid != DEFAULT_STRING && uuid != EMPTY_UUID_STRING)
                        {
                            emptyUuid = false;
                            sb.Append(uuid);
                            sbShort.Append(uuid);
                        }
                    }
                    if(emptyUuid)
                    {//fallback to Disk ID
                        LoggerUtil.GetAppWideLogger()?.Info("Fingerprint Fallback to DISK ID");

                        searcher = new ManagementObjectSearcher("SELECT SerialNumber, Model FROM Win32_DiskDrive");

                        foreach (ManagementObject disk in searcher.Get())
                        {
                            var serial = disk["SerialNumber"]?.ToString()?.Trim();
                            if (serial != NOT_APPLICABLE_STRING && serial != DEFAULT_STRING)
                            {
                                sb.Append(serial);
                                sbShort.Append(serial);
                            }
                        }
                    }
                }

                collection.Dispose();
                searcher.Dispose();

                LoggerUtil.GetAppWideLogger()?.Info("Fingerprint seed is " + sb.ToString());
                LoggerUtil.GetAppWideLogger()?.Info("Fingerprint short seed is " + sbShort.ToString());
                byte[] bt = sec.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                fingerPrintLong = BitConverter.ToString(bt).Replace("-", "");
                
                bt = sec.ComputeHash(Encoding.UTF8.GetBytes(sbShort.ToString()));
                fingerPrintShort = BitConverter.ToString(bt).Replace("-", "");

                LoggerUtil.GetAppWideLogger()?.Info("My fingerprint is {0} and {1}", fingerPrintLong, fingerPrintShort);
            }
        }

        /// <summary>
        /// Container for the device unique ID.
        /// </summary>
        private static string fingerPrintLong;
        private static string fingerPrintShort;

        /// <summary>
        /// Gets a unique identifier for this device based on miscelleneous unique ID's.
        /// </summary>
        public string Value
        {
            get
            {
                return fingerPrintLong;
            }
        }

        public string Value2
        {
            get
            {
                return fingerPrintShort;
            }
        }
    }
}