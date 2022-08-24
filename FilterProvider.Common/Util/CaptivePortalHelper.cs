/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using CloudVeil.Core.Windows.Util;
using Filter.Platform.Common;
using Filter.Platform.Common.Util;
using FilterProvider.Common.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilterProvider.Common.Util
{
    /// <summary>
    /// Tri-state detection enum for checkCaptivePortalState()
    /// </summary>
    public enum CaptivePortalDetected
    {
        /// <summary>
        /// Equivalent to (bool)false
        /// </summary>
        No,

        /// <summary>
        /// Equivalent to (bool)true
        /// </summary>
        Yes,

        /// <summary>
        /// This is returned when windows has detected a network address change but no response has been returned from connectivitycheck.cloudveil.org
        /// </summary>
        NoResponseReturned
    }

    public class CaptivePortalHelper
    {
        private const long CACHE_TIMEOUT_TICKS = 12*TimeSpan.TicksPerHour;

        public static CaptivePortalHelper Default { get; private set; }

        static CaptivePortalHelper()
        {
            Default = new CaptivePortalHelper();
        }

        private string[] currentCaptivePortalSSIDs = null;
        private DateTime captivePortalDetectedAt = DateTime.MinValue;

        private IWifiManager wifiManager;

        public CaptivePortalHelper()
        {
            try
            {
                wifiManager = PlatformTypes.New<IWifiManager>();
            }
            catch(Exception ex)
            {
                LoggerUtil.GetAppWideLogger().Error(ex, "Failed to initialize WIFI Manager.");
            }
        }

        /// <summary>
        /// Gets a list of all SSIDs that the filter is currently connected to.
        /// SSIDs will be stored in a file when captive portal is detected.
        /// </summary>
        /// <returns>List of any SSIDs that the computer is connected to. The count of this list will most likely be 1.</returns>
        private string[] detectCurrentSSIDs()
        {
            return wifiManager?.DetectCurrentSsids()?.ToArray() ?? new string[0];
        }

        private object captivePortalSettingsLock = new object();

        private IPathProvider paths = PlatformTypes.New<IPathProvider>();

        private string portalSettingsPath
        {
            get => Path.Combine(paths.ApplicationDataFolder, "captive-portal-settings.dat");
        }

        private void deleteCaptivePortalSSIDsFile()
        {
            try
            {
                lock (captivePortalSettingsLock)
                {
                    if (File.Exists(portalSettingsPath))
                    {
                        File.Delete(portalSettingsPath);
                    }
                }
            }
            catch (Exception e)
            {
                var logger = LoggerUtil.GetAppWideLogger();

                logger.Error("Error occurred while deleting captive portal file.");
                LoggerUtil.RecursivelyLogException(logger, e);
            }
        }

        private void loadCaptivePortalSSIDs()
        {
            try
            {
                lock (captivePortalSettingsLock)
                {
                    if(!File.Exists(portalSettingsPath))
                    {
                        return;
                    }

                    using (Stream fileStream = File.Open(portalSettingsPath, FileMode.Open))
                    {
                        StreamReader reader = new StreamReader(fileStream);
                        string ssidLine = reader.ReadLine();
                        string dateLine = reader.ReadLine();

                        if (ssidLine == null || dateLine == null)
                        {
                            currentCaptivePortalSSIDs = null;
                            return;
                        }

                        currentCaptivePortalSSIDs = ssidLine.Split(',').Select(s => Encoding.ASCII.GetString(Convert.FromBase64String(s))).ToArray();
                        captivePortalDetectedAt = DateTime.Parse(dateLine);

                        deleteCacheIfExpired();
                    }
                }
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
            }
        }

        private void deleteCacheIfExpired()
        {
            if (captivePortalDetectedAt + new TimeSpan(CACHE_TIMEOUT_TICKS) < DateTime.Now)
            {
                deleteCaptivePortalSSIDsFile();
                currentCaptivePortalSSIDs = null;
            }
        }

        private void saveCaptivePortalSSIDs(string[] ssids, DateTime captivePortalDetectionTime)
        {
            try
            {
                lock (captivePortalSettingsLock)
                {
                    using (Stream fileStream = File.Open(portalSettingsPath, FileMode.Create))
                    {
                        StreamWriter writer = new StreamWriter(fileStream);

                        // Encoding to base-64 as a lazy form of escaping commas (our delimiter) in the stored SSIDs.
                        string ssidLine = string.Join(",", ssids.Select(s => Convert.ToBase64String(Encoding.ASCII.GetBytes(s))));
                        string dateLine = captivePortalDetectionTime.ToString("o");

                        writer.WriteLine(ssidLine);
                        writer.WriteLine(dateLine);
                        writer.Close();
                    }
                }
            }
            catch (Exception e)
            {
                var logger = LoggerUtil.GetAppWideLogger();
                logger.Error("Failed to save captive portal SSIDs");

                LoggerUtil.RecursivelyLogException(logger, e);
            }
        }

        /// <summary>
        /// This function should be called whenever a captive portal is detected.
        /// </summary>
        /// 
        /// <remarks>
        /// Gets list of SSIDs the computer is currently connected to, and saves them to disk.
        /// In format:
        /// SSID1,SSID2\n
        /// ISO8601-Date-of-captive-portal-detected
        /// 
        /// Also keeps representation of SSIDs in memory.
        /// </remarks>
        public void OnCaptivePortalDetected()
        {
            try
            {
                currentCaptivePortalSSIDs = detectCurrentSSIDs();
                captivePortalDetectedAt = DateTime.Now;

                saveCaptivePortalSSIDs(currentCaptivePortalSSIDs, captivePortalDetectedAt);
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
            }
        }

        /// <summary>
        /// Used to determine whether we should keep users on DHCP or force them back to our DNS settings (in event of captive portal networks)
        /// </summary>
        /// <remarks>
        /// This function checks our current SSID against current SSIDs that were detected as captive portals.
        /// 
        /// If the in-memory SSID list is null, this function looks for a file with the SSID in it.
        /// 
        /// If SSID information is older than 12 hours and we are not on any SSIDs in that list, we discard the list.
        /// </remarks>
        /// <returns>true if current network was a captive portal, false otherwise</returns>
        public bool IsCurrentNetworkCaptivePortal()
        {
            try
            {
                if (currentCaptivePortalSSIDs == null)
                {
                    loadCaptivePortalSSIDs();
                }

                deleteCacheIfExpired();

                if (currentCaptivePortalSSIDs == null)
                {
                    return false;
                }

                string[] currentSSIDs = detectCurrentSSIDs();
                if(currentSSIDs == null)
                {
                    return false;
                }

                foreach (string currentSSID in currentSSIDs)
                {
                    foreach(string SSID in currentCaptivePortalSSIDs)
                    {
                        if(currentSSID == SSID)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
                return false;
            }
        }
    }
}
