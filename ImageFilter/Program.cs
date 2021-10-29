using System;
using System.Threading;
using Topshelf;

namespace ImageFilter
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

            if (createdNew)
            {
                // Having problems with the service not starting? Run FilterServiceProvider.exe test-me in admin mode to figure out why.
                if (args.Length > 0 && args[0] == "test")
                {
                    var server = new Server();
                    server.Start(Server.PORT);

                    var exiting = false;
                    AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
                    {
                        exiting = true;
                    };

                    while (!exiting)
                    {
                        Thread.Sleep(1000);
                    }
                }
                else
                {
                    var exitCode = HostFactory.Run(x =>
                    {
                        x.Service<Server>(s =>
                        {
                            s.ConstructUsing(name => new Server());
                            s.WhenStarted((server, hostCtl) => server.Start(Server.PORT));
                            s.WhenStopped((server, hostCtl) => {
                                return server.Stop();
                            });
                        });

                        x.EnableShutdown();
                        x.SetDescription("Image Filtering Service");
                        x.SetDisplayName(nameof(Server));
                        x.SetServiceName(nameof(Server));
                        x.StartAutomatically();

                        x.RunAsLocalSystem();
                    });
                }

                InstanceMutex.ReleaseMutex();
            }
            else
            {
                Console.WriteLine("Service already running. Exiting.");

                // We have to exit with a safe code so that if another monitor is running, it won't
                // see this process end and then panic and try to restart it when it's already running!

                Environment.Exit(1);
            }

            if (InstanceMutex != null)
            {
                InstanceMutex.Dispose();
            }
        }
    }
}
