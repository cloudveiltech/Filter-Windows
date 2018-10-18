using System;
using System.Threading;
using System.IO;
using System.Reflection;

namespace FilterServiceProvider.Mac
{
    public static class MainClass
    {
        public static bool KeepRunning { get; set; }

        // Since we're using a mac app bundle, NLog doesn't know where to find
        // its configuration file. Find it for NLog and load it.
        static void LoadNLogConfiguration()
        {
            string currentAssemblyPath = Assembly.GetCallingAssembly().Location;
            string resourcesPath = Path.Combine(Path.GetDirectoryName(currentAssemblyPath), "..", "Resources");
            resourcesPath = Path.GetFullPath(resourcesPath);

            string nlogConfig = Path.Combine(resourcesPath, "NLog.config");

            if(!File.Exists(nlogConfig))
            {
                Console.WriteLine("No NLog.config found.");
            }
            else
            {
                NLog.LogManager.LoadConfiguration(nlogConfig);
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Daemon App!");

            LoadNLogConfiguration();

            Services.FilterServiceProvider provider = new Services.FilterServiceProvider();
            provider.Start();

            KeepRunning = true;

            while(KeepRunning)
            {
                Thread.Sleep(250);
            }
        }
    }
}
