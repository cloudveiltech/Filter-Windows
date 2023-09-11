﻿/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeil.Core.Windows.Util;
using Filter.Platform.Common.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Topshelf;
using CloudVeil.Core.Windows.Services;

namespace CloudVeilService.Services
{
    internal class Warden : BaseProtectiveService
    {
        public Warden() : base("Sentinel", true)
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

    public class WardenProgram
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
                var exitCode = HostFactory.Run(x =>
                {
                    x.Service<Warden>(s =>
                    {
                        s.ConstructUsing(name => new Warden());
                        s.WhenStarted((guardian, hostCtl) => guardian.Start());
                        s.WhenStopped((guardian, hostCtl) => guardian.Stop());

                        s.WhenShutdown((guardian, hostCtl) =>
                        {
                            hostCtl.RequestAdditionalTime(TimeSpan.FromSeconds(30));
                            guardian.Shutdown(ExitCodes.ShutdownWithSafeguards);
                        });
                    });
                    x.SetDescription("Content Filtering Enforcement Service");
                    x.SetDisplayName(nameof(Warden));
                    x.SetServiceName(nameof(Warden));
                    x.StartAutomatically();
                    x.RunAsLocalSystem();
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
