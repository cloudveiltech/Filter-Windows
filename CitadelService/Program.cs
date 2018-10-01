/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CitadelService.Services;
using Filter.Platform.Common.Util;
using System;
using System.Threading;
using Topshelf;

namespace CitadelService
{
    internal class Program
    {
        private static Mutex InstanceMutex;

        private static void Main(string[] args)
        {
            string appVerStr = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            appVerStr += "." + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString();

            bool createdNew;
            InstanceMutex = new Mutex(true, string.Format(@"Global\{0}", appVerStr.Replace(" ", "")), out createdNew);

            bool exiting = false;

            if(createdNew)
            {
                // Having problems with the service not starting? Run FilterServiceProvider.exe test-me in admin mode to figure out why.
                if (args.Length > 0 && args[0] == "test-me")
                {
                    FilterServiceProvider provider = new FilterServiceProvider();
                    provider.Start();
                    AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
                    {
                        exiting = true;
                    };

                    while(!exiting)
                    {
                        Thread.Sleep(1000);
                    }
                }
                else
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

                            // When someone logs on, start up a GUI for them.
                            s.WhenSessionChanged((fsp, hostCtl) => fsp.OnSessionChanged());
                        });

                        x.EnableShutdown();
                        x.SetDescription("Content Filtering Service");
                        x.SetDisplayName(nameof(FilterServiceProvider));
                        x.SetServiceName(nameof(FilterServiceProvider));
                        x.StartAutomatically();

                        x.RunAsLocalSystem();

                        // We don't need recovery options, because there will be multiple services all
                        // watching eachother that will all record eachother in the event of a failure or
                        // forced termination.
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

                InstanceMutex.ReleaseMutex();
            }
            else
            {
                Console.WriteLine("Service already running. Exiting.");

                // We have to exit with a safe code so that if another monitor is running, it won't
                // see this process end and then panic and try to restart it when it's already running!

                Environment.Exit((int)ExitCodes.ShutdownWithSafeguards);
            }

            if(InstanceMutex != null)
            {
                InstanceMutex.Dispose();
            }
        }
    }
}