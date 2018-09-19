/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Util;
using Filter.Platform.Common.Util;
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
    internal class FilterStarter : BaseProtectiveService
    {
        // TARGET_APPLICATION_NAME replaced by the name of the process targeted on compilation.
        public FilterStarter() : base("TARGET_APPLICATION_NAME", true, false)
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

    public class FilterStarterProgram
    {
        private static Mutex InstanceMutex;

        static void Main(string[] args)
        {

            string appVerStr = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            appVerStr += "." + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString();

            bool createdNew;
            InstanceMutex = new Mutex(true, string.Format(@"Global\{0}", appVerStr.Replace(" ", "")), out createdNew);

            var starter = new FilterStarter();
            starter.EnsureAlreadyRunning();
        }
    }
}
