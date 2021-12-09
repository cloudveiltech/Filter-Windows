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
        const float DEFAULT_UNSAFE = 0.6f;
        public float SafeThreshold { get; set; } = DEFAULT_SAFE;

        public float UnsafeThreshold { get; set; } = DEFAULT_UNSAFE;

        public static Config Load(string filePath, Logger logger)
        {
            Config config = new Config();
            if (File.Exists(filePath)) { 
                string jsonString = File.ReadAllText(filePath);
                dynamic parsed = JsonConvert.DeserializeObject(jsonString);
                config.SafeThreshold = parsed.safeThreshold;
                config.UnsafeThreshold = parsed.unsafeThreshold;
                logger.Info("Config loaded from file: safe (" + config.SafeThreshold+ "), unsafe(" + config.UnsafeThreshold + ")");
            } else
            {
                logger.Info("Can't find config, use default values");
            }
            return config;
        }
    }
}
