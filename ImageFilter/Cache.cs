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
    //https://stackoverflow.com/questions/21456215/persisting-memorycache-content-to-file
    class CachedMemory
    {
        Dictionary<String, bool> cache = new Dictionary<String, bool>();
        private String persistenceFilePath = null;
        private int cacheSizeLimit;

        private DateTime lastPersistTime = DateTime.MinValue;
        const double SAVE_CACHE_TIMEOUT_SEC = 60;

        public Logger Logger { get; set; }

        private void log(string msg)
        {
            if(Logger != null)
            {
                Logger.Info(msg);
            }
        }
        public int getCacheSize()
        {
            return this.cache.Count;
        }


        public CachedMemory(int cacheSizeLimit, string cacheFilePath, Logger logger)
        {
            Logger = logger;
            InitializeCache(cacheFilePath, cacheSizeLimit);
        }

        private void InitializeCache(string cacheFilePath, int cacheSizeLimit)
        {
            this.cacheSizeLimit = cacheSizeLimit;
            this.persistenceFilePath = cacheFilePath;
            if (File.Exists(cacheFilePath))
            {
                try
                {
                    log("Cache loading started");
                    using (FileStream fileStream = new FileStream(cacheFilePath, FileMode.Open))
                    {
                        IFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                        this.cache = (Dictionary<String, bool>)bf.Deserialize(fileStream);
                        log("Cache loaded " + cache.Keys.Count);
                        fileStream.Close();
                    }
                } catch(Exception e) {
                    log(e.ToString());
                }
            }
            

            if (this.cache.Keys.Count > this.cacheSizeLimit)
            {
                int difference = this.cache.Keys.Count - this.cacheSizeLimit;

                for (int i = 0; i < difference; i++)
                {
                    cache.Remove(cache.Keys.First());
                }
            }
        }

        public bool TryGetValue(string key, out bool res)
        {
            return cache.TryGetValue(key, out res);
        }

        public void Add(string key, bool value)
        {
            this.cache.Add(key, value);
            if(DateTime.UtcNow - lastPersistTime > TimeSpan.FromSeconds(SAVE_CACHE_TIMEOUT_SEC))
            {
                Persist(); 
            }
        }

        public void Persist()
        {
            if (this.cache.Keys.Count > this.cacheSizeLimit)
            {
                int difference = this.cache.Keys.Count - this.cacheSizeLimit;

                for (int i = 0; i < difference; i++)
                {
                    cache.Remove(cache.Keys.First());
                }
            }

            using (FileStream fileStream = new FileStream(persistenceFilePath, FileMode.Create))
            {
                IFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                bf.Serialize(fileStream, this.cache);
                fileStream.Close();
            }

            this.lastPersistTime = DateTime.UtcNow;
            log("Cache saved");
        }
    }
}
