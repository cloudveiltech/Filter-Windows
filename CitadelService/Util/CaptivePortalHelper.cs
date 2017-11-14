using Citadel.Core.Windows.Util;
using NativeWifi;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitadelService.Util
{
    public class CaptivePortalHelper
    {
        private static CaptivePortalHelper s_instance;
        public static CaptivePortalHelper Default { get { return s_instance; } }

        static CaptivePortalHelper()
        {
            s_instance = new CaptivePortalHelper();
        }

        private string[] m_currentCaptivePortalSSIDs = null;
        private DateTime m_captivePortalDetectedAt = DateTime.MinValue;

        /// <summary>
        /// Gets a list of all SSIDs that the filter is currently connected to.
        /// SSIDs will be stored in a file when captive portal is detected.
        /// </summary>
        /// <returns>List of any SSIDs that the computer is connected to. The count of this list will most likely be 1.</returns>
        private string[] detectCurrentSSIDs()
        {
            try
            {
                WlanClient wlanClient = new WlanClient();
                List<string> connectedSsids = new List<string>();

                foreach (WlanClient.WlanInterface wlanInterface in wlanClient.Interfaces)
                {
                    Wlan.Dot11Ssid ssid = wlanInterface.CurrentConnection.wlanAssociationAttributes.dot11Ssid;
                    connectedSsids.Add(new string(Encoding.ASCII.GetChars(ssid.SSID, 0, (int)ssid.SSIDLength)));
                }

                return connectedSsids.ToArray();
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
                return null;
            }
        }

        private object m_captivePortalSettingsLock = new object();
        
        private string portalSettingsPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"CloudVeil\captive-portal-settings.dat");
            }
        }

        private void deleteCaptivePortalSSIDsFile()
        {
            try
            {
                lock (m_captivePortalSettingsLock)
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
                lock (m_captivePortalSettingsLock)
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
                            m_currentCaptivePortalSSIDs = null;
                            return;
                        }

                        m_currentCaptivePortalSSIDs = ssidLine.Split(',').Select(s => Encoding.ASCII.GetString(Convert.FromBase64String(s))).ToArray();
                        m_captivePortalDetectedAt = DateTime.Parse(dateLine);
                    }
                }
            }
            catch (Exception e)
            {
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
            }
        }

        private void saveCaptivePortalSSIDs(string[] ssids, DateTime captivePortalDetectionTime)
        {
            try
            {
                lock (m_captivePortalSettingsLock)
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
                m_currentCaptivePortalSSIDs = detectCurrentSSIDs();
                m_captivePortalDetectedAt = DateTime.Now;

                saveCaptivePortalSSIDs(m_currentCaptivePortalSSIDs, m_captivePortalDetectedAt);
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
                if (m_currentCaptivePortalSSIDs == null)
                {
                    loadCaptivePortalSSIDs();
                }

                if(m_currentCaptivePortalSSIDs == null)
                {
                    return false;
                }

                string[] currentSSIDs = detectCurrentSSIDs();
                if(currentSSIDs == null)
                {
                    return false;
                }

                foreach(string currentSSID in currentSSIDs)
                {
                    foreach(string SSID in m_currentCaptivePortalSSIDs)
                    {
                        if(currentSSID == SSID)
                        {
                            return true;
                        }
                    }
                }

                if (m_captivePortalDetectedAt + new TimeSpan(12, 0, 0) < DateTime.Now)
                {
                    deleteCaptivePortalSSIDsFile();
                    m_currentCaptivePortalSSIDs = null;
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
