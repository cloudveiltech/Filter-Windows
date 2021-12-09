using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ImageFilter
{
    class Config
    {
        const float DEFAULT_SAFE = 0.9f;
        const float DEFAULT_UNSAFE = 0.2f;
        DateTime lastLoadTime = DateTime.Now;
        private string filePath;
        private Logger logger;

        public Config(string filePath, Logger logger)
        {
            this.filePath = filePath;
            this.logger = logger;
        }

        public float SafeThreshold { get; set; } = DEFAULT_SAFE;

        public float UnsafeThreshold { get; set; } = DEFAULT_UNSAFE;

        public async void CheckAndReload()
        {
            await Task.Run(() =>
            {
                if (File.Exists(filePath))
                {
                    if (File.GetLastWriteTime(filePath) > lastLoadTime)
                    {
                        Reload();
                    }
                }
            });            
        }
        public void Reload()
        {
            try
            {
                if (File.Exists(filePath))
                {
                    string jsonString = File.ReadAllText(filePath);
                    dynamic parsed = JsonConvert.DeserializeObject(jsonString);
                    SafeThreshold = parsed.safeThreshold;
                    UnsafeThreshold = parsed.unsafeThreshold;
                    lastLoadTime = DateTime.Now;
                    checkValid();
                    logger.Info("Config loaded from file: safe (" + SafeThreshold + "), unsafe(" + UnsafeThreshold + ")");
                }
                else
                {
                    logger.Info("Can't find config, use default values");
                }
            } 
            catch(Exception e)
            {
                logger.Info("Can't load config");
                logger.Info(e);
            }
        }

        private void checkValid()
        {
            if(SafeThreshold <= 0 || SafeThreshold >= 1)
            {
                logger.Info("SafeThreshold should be between 0 and 1");
                SafeThreshold = DEFAULT_SAFE;
            }
            if (UnsafeThreshold <= 0 || UnsafeThreshold >= 1)
            {
                logger.Info("UnsafeThreshold should be between 0 and 1");
                UnsafeThreshold = DEFAULT_UNSAFE;
            }
        }
    }
}
