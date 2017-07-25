using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CitadelService.Services;
using Topshelf;
using Citadel.Core.Windows.Util;

namespace CitadelService
{
    class Program
    {
        private static Mutex InstanceMutex;

        static void Main(string[] args)
        {
            string appVerStr = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            appVerStr += "." + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString();

            bool createdNew;
            InstanceMutex = new Mutex(true, string.Format(@"Global\{0}", appVerStr.Replace(" ", "")), out createdNew);

            if(createdNew)
            {
                var exitCode = HostFactory.Run(x =>
                {
                    x.Service<FilterServiceProvider>(s =>
                    {
                        s.ConstructUsing(name => new FilterServiceProvider());
                        s.WhenStarted((fsp, hostCtl) => fsp.Start());
                        s.WhenStopped((fsp, hostCtl) => fsp.Stop());
                        s.WhenShutdown((fsp, hostCtl) =>
                        {
                            hostCtl.RequestAdditionalTime(TimeSpan.FromSeconds(30));
                            fsp.Shutdown();
                        });
                    });

                    x.EnableShutdown();
                    x.SetDescription("Content Filtering Service");
                    x.SetDisplayName(nameof(FilterServiceProvider));
                    x.SetServiceName(nameof(FilterServiceProvider));
                    x.StartAutomatically();

                    x.RunAsLocalSystem();

                    // We don't need recovery options, because there will be multiple
                    // services all watching eachother that will all record eachother
                    // in the event of a failure or forced termination.
                    /*
                    //http://docs.topshelf-project.com/en/latest/configuration/config_api.html#id1
                    x.EnableServiceRecovery(rc =>
                    {
                        rc.OnCrashOnly();
                        rc.RestartService(0);
                        rc.RestartService(0);
                        rc.RestartService(0);                        
                    });
                    */
                });
            }
            else
            {
                Console.WriteLine("Service already running. Exiting.");

                // We have to exit with a safe code so that if another
                // monitor is running, it won't see this process end
                // and then panic and try to restart it when it's already
                // running!
                
                Environment.Exit((int)ExitCodes.ShutdownWithSafeguards);
            }
        }
    }
}
