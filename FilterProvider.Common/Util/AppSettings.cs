using Filter.Platform.Common;
using Filter.Platform.Common.Types;
using Filter.Platform.Common.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FilterProvider.Common.Util
{ 
    public class AppSettings
    {
        private static object appSettingsLock = new object();

        public DateTime? RemindLater { get; set; }

        public DateTime? LastUpdateCheck { get; set; }
        public UpdateCheckResult? UpdateCheckResult { get; set; }

        public DateTime? LastSettingsCheck { get; set; }
        public ConfigUpdateResult? ConfigUpdateResult { get; set; }

        public static AppSettings Default { get; private set; }

        static AppSettings()
        {
            Default = Load();
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
                    return loaded;
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
