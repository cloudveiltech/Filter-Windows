using Filter.Platform.Common;
using Filter.Platform.Common.IPC.Messages;
using Filter.Platform.Common.Types;
using Filter.Platform.Common.Util;
using FilterProvider.Common.Proxy;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FilterProvider.Common.Util
{ 
    public class AppSettings
    {
        private const ushort MIN_PORT_VALUE = 10000; 
        private const ushort MAX_PORT_VALUE = 20000;
        private const ushort DEFAULT_HTTP_PORT = 15300;
        private const ushort DEFAULT_HTTPS_PORT = 15301;
        private const ushort DEFAULT_CONFIG_PORT = 14299;


        private static object appSettingsLock = new object();

        public DateTime? RemindLater { get; set; }

        public DateTime? LastUpdateCheck { get; set; }
        public UpdateCheckResult? UpdateCheckResult { get; set; }

        public DateTime? LastSettingsCheck { get; set; }
        public ConfigUpdateResult? ConfigUpdateResult { get; set; }
        public BugReportSetting BugReportSettings { get; set; } = new BugReportSetting(false, false);
        
        public ushort ConfigServerPort { get; set; } = DEFAULT_CONFIG_PORT;
        public ushort HttpsPort { get; set; } = DEFAULT_HTTPS_PORT;
        public ushort HttpPort { get; set; } = DEFAULT_HTTP_PORT;
        public bool RandomizePorts { get; set; } = false;

        public static AppSettings Default { get; private set; }
        

        static AppSettings()
        {
            Default = Load();
        }

        public void ShufflePorts()
        {
            while (!TryShufflePorts()) { }
        }

        public void SetDefaultPorts()
        {
            ConfigServerPort = DEFAULT_CONFIG_PORT;
            HttpsPort = DEFAULT_HTTPS_PORT;
            HttpPort = DEFAULT_HTTP_PORT;
        }

        private bool TryShufflePorts()
        {            
            ConfigServerPort = CommonProxyServer.GetRandomFreePort(MIN_PORT_VALUE, MAX_PORT_VALUE);
            HttpPort = CommonProxyServer.GetRandomFreePort(MIN_PORT_VALUE, MAX_PORT_VALUE);
            HttpsPort = CommonProxyServer.GetRandomFreePort(MIN_PORT_VALUE, MAX_PORT_VALUE);

            return ConfigServerPort != HttpPort && 
                ConfigServerPort != HttpsPort && 
                HttpPort != HttpsPort;
        }

        public void Save()
        {
            lock(appSettingsLock)
            {
                string json = null;

                try
                {
                    json = JsonConvert.SerializeObject(this);
                }
                catch (Exception ex)
                {
                    LoggerUtil.GetAppWideLogger().Error(ex, "Failed to convert object to JSON.");
                    return;
                }

                try
                {
                    IPathProvider paths = PlatformTypes.New<IPathProvider>();

                    string settingsPath = paths.GetPath(@"app.settings");
                    using (StreamWriter writer = new StreamWriter(File.Open(settingsPath, FileMode.Create)))
                    {
                        writer.Write(json);
                    }
                }
                catch(Exception ex)
                {
                    LoggerUtil.GetAppWideLogger().Error(ex, "Failed to save app settings.");
                }
            }
        }

        public static AppSettings Load()
        {
            lock(appSettingsLock)
            {
                IPathProvider paths = PlatformTypes.New<IPathProvider>();

                string settingsPath = paths.GetPath(@"app.settings");

                if(!File.Exists(settingsPath))
                {
                    return new AppSettings();
                }

                using (StreamReader reader = File.OpenText(settingsPath))
                {
                    string json = reader.ReadToEnd();
                    AppSettings loaded = JsonConvert.DeserializeObject<AppSettings>(json);
                    return loaded ?? new AppSettings();
                }
            }
        }

        public bool CanUpdate()
        {
            if (RemindLater == null || DateTime.Now >= RemindLater.Value)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
