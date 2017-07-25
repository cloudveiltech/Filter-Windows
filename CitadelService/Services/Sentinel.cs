/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Te.Citadel.Services;
using Topshelf;

namespace CitadelService.Services
{
    internal class Sentinel : BaseProtectiveService
    {
        public Sentinel() : base("TARGET_APPLICATION_NAME", true)
        {

        }

        public bool Start()
        {
            return true;
        }

        public bool Stop()
        {
            return false;
        }

        public override void Shutdown(ExitCodes code)
        {
            // Quit our application with a safe code so that whoever
            // is watching us knows it's time to shut down.
            Environment.Exit((int)code);
        }
    }

    public class SentinelProgram
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
                    x.Service<Sentinel>(s =>
                    {
                        s.ConstructUsing(name => new Sentinel());
                        s.WhenStarted((guardian, hostCtl) => guardian.Start());
                        s.WhenStopped((guardian, hostCtl) => guardian.Stop());
                        
                        s.WhenShutdown((guardian, hostCtl) =>
                        {
                            hostCtl.RequestAdditionalTime(TimeSpan.FromSeconds(30));
                            guardian.Shutdown(ExitCodes.ShutdownWithSafeguards);
                        });

                    });

                    x.EnableShutdown();
                    x.SetDescription("Content Filtering Enforcement Service");
                    x.SetDisplayName(nameof(Sentinel));
                    x.SetServiceName(nameof(Sentinel));
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
