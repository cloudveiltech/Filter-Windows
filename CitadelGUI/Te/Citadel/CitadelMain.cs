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
using System.Threading.Tasks;
using System.Text;
using Citadel.Core.WinAPI;
using Citadel.Core.Windows.Util;
using Citadel.IPC;

namespace Te.Citadel
{
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
                try
                {
                    InstanceMutex = new Mutex(true, string.Format(@"Local\{0}", GuidUtility.Create(GuidUtility.DnsNamespace, appVerStr).ToString("B")), out createdNew);
                }
                catch
                {
                    // We can get access denied if SYSTEM is running this.
                    createdNew = false;
                }
                
                if(!createdNew)
                {
                    try
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
                    }
                    catch(Exception e)
                    {
                        LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
                    }


                    // In case we have some out of sync state where the app is running at a higher
                    // privilege level than us, the app won't get our messages. So, let's attempt
                    // an IPC named pipe to deliver the message as well.
                    try
                    {
                        using(var ipcClient = new IPCClient())
                        {
                            ipcClient.RequestPrimaryClientShowUI();

                            // Wait plenty of time before dispose to allow delivery of the msg.
                            Task.Delay(500).Wait();
                        }
                    }
                    catch(Exception e)
                    {
                        // The only way we got here is if the server isn't running, in
                        // which case we can do nothing because its beyond our domain.
                        LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
                    }

                    // Close this instance.
                    Environment.Exit(-1);
                    return;
                }
            }
            catch(Exception e)
            {
                // The only way we got here is if the server isn't running, in
                // which case we can do nothing because its beyond our domain.
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), e);
                return;
            }

            try
            {
                MainLogger = LoggerUtil.GetAppWideLogger();
            }
            catch { }

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
                try
                {
                    MainLogger = LoggerUtil.GetAppWideLogger();
                    LoggerUtil.RecursivelyLogException(MainLogger, e);
                }
                catch(Exception be)
                {
                    var sb = new StringBuilder();
                    while(e != null)
                    {
                        sb.AppendLine(e.Message);
                        sb.AppendLine(e.StackTrace);
                        e = e.InnerException;
                    }

                    while(be != null)
                    {
                        sb.AppendLine(be.Message);
                        sb.AppendLine(be.StackTrace);
                        be = be.InnerException;
                    }

                    File.WriteAllText(@"C:\CloudVeil.log", sb.ToString());
                }
            }

            // No matter what, always ensure that critical flags are removed from our process before
            // exiting.
            ProcessProtection.Unprotect();
            
        }
    }
}