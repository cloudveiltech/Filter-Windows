/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using NLog;
using System;
using System.IO;
using Te.Citadel.Util;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Linq;
using Te.Citadel.WinApi;

namespace Te.Citadel
{
    /// <summary>
    /// Various exit codes indicating the reason for a shutdown.
    /// </summary>
    public enum ExitCodes : int
    {
        ShutdownWithSafeguards,
        ShutdownWithoutSafeguards = 100,
    }

    public static class CitadelMain
    {
        private static Logger MainLogger;

        private static Mutex InstanceMutex = null;

        /// <summary>
        /// </summary>
        /// <param name="args">
        /// </param>
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                string appVerStr = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                appVerStr += "." + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString();

                bool createdNew;
                InstanceMutex = new Mutex(true, string.Format(@"Global\{0}", GuidUtility.Create(GuidUtility.DnsNamespace, appVerStr).ToString("B")), out createdNew);
                if(!createdNew)
                {
                    var thisProcess = Process.GetCurrentProcess();
                    var processes = Process.GetProcessesByName(thisProcess.ProcessName).Where(p => p.Id != thisProcess.Id);

                    foreach(Process runningProcess in processes)
                    {
                        foreach(var handle in WindowHelpers.EnumerateProcessWindowHandles(runningProcess.Id))
                        {
                            // Send window show.
                            WindowHelpers.SendMessage(handle, (uint)WindowMessages.SHOWWINDOW, 9, 0);
                        }
                    }

                    // Close this instance.
                    System.Windows.Application.Current.Shutdown(-1);
                    return;
                }
            }
            catch(Exception e)
            {   
                return;
            }

            try
            {
                // Let's always overwrite the NLog config with our packed version to ensure
                // that this doesn't get screwed or tampered easily.\
                var nlogCfgPath = AppDomain.CurrentDomain.BaseDirectory + @"NLog.config";

                var nlogCfgUri = new Uri("pack://application:,,,/Resources/NLog.config");
                var resourceStream = System.Windows.Application.GetResourceStream(nlogCfgUri);
                TextReader tsr = new StreamReader(resourceStream.Stream);
                var nlogConfigText = tsr.ReadToEnd();
                resourceStream.Stream.Close();
                resourceStream.Stream.Dispose();
                File.WriteAllText(nlogCfgPath, nlogConfigText);

                MainLogger = LoggerUtil.GetAppWideLogger();
            }
            catch
            {
                // What can be done? WHAT. CAN. BE. DONE!?!?! X(
            }

            try
            {  
                var app = new CitadelApp();
                app.InitializeComponent();
                app.Run();

                // Always release mutex.
                if(InstanceMutex != null)
                {
                    InstanceMutex.ReleaseMutex();
                }
            }
            catch(Exception e)
            {
                MainLogger = LoggerUtil.GetAppWideLogger();
                LoggerUtil.RecursivelyLogException(MainLogger, e);
            }

            // No matter what, always ensure that critical flags are removed from our process before
            // exiting.
            ProcessProtection.Unprotect();
            
        }
    }
}