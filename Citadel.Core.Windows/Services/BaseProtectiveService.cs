/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;

namespace Te.Citadel.Services
{
    /// <summary>
    /// This class is a base service designed with the sole focus to monitor a process and restart it
    /// if it ends without authorization, or start it if it's not running at all.
    /// </summary>
    public abstract class BaseProtectiveService
    {
        /// <summary>
        /// The base directory where we are to launch the process we're protecting from if we need to. 
        /// </summary>
        private readonly string m_baseDirectory;

        /// <summary>
        /// The absolute path to the binary for the process that we're watching. 
        /// </summary>
        private readonly string m_processBinaryAbsPath;

        /// <summary>
        /// The process name of the process we are to keep alive. 
        /// </summary>
        private readonly string m_processToWatch;

        /// <summary>
        /// Whether or not our target is a service. 
        /// </summary>
        private readonly bool m_isTargetService;

        private Process m_processHandle = null;

        /// <summary>
        /// Creates a new instance of a base process protecting service with the name of another
        /// process we are responsible for watching and keeping alive.
        /// </summary>
        /// <param name="processNameToObserve">
        /// The name of the process we are to observe and protect.
        /// </param>
        /// <param name="isService">
        /// Whether or not the process we are protecting is a service.
        /// </param>
        public BaseProtectiveService(string processNameToObserve, bool isService)
        {
            m_processToWatch = processNameToObserve;
            m_baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            m_processBinaryAbsPath = string.Format("{0}{1}.exe", m_baseDirectory, processNameToObserve);

            m_isTargetService = isService;

            EnsureAlreadyRunning();
        }

        private void EnsureAlreadyRunning()
        {
            foreach(var proc in Process.GetProcesses())
            {   
                if(proc.ProcessName.Equals(m_processToWatch, StringComparison.OrdinalIgnoreCase))
                {
                    // Found the process already alive. Return and do nothing.
                    SetProcessHandle(proc);
                    return;
                }
            }

            // Didn't find the process alive. Start it.
            Resuscitate();
        }

        private void SetProcessHandle(Process proc)
        {
            if(m_processHandle != null)
            {
                try
                {
                    m_processHandle.Exited -= OnProcExit;
                }
                catch { }
            }

            m_processHandle = proc;
            m_processHandle.EnableRaisingEvents = true;
            m_processHandle.Exited += OnProcExit;
        }

        private void OnProcExit(object sender, EventArgs e)
        {
            var exitCode = -1;
            if(m_processHandle != null)
            {
                exitCode = m_processHandle.ExitCode;
            }

            if(exitCode < (int)ExitCodes.ShutdownWithSafeguards || exitCode >= int.MaxValue || exitCode > (int)ExitCodes.ShutdownWithoutSafeguards)
            {
                Resuscitate();
            }
            else
            {
                Shutdown((ExitCodes)exitCode);
            }
        }

        protected virtual void Resuscitate()
        {
            // No try/catch here beacuse we want the service to die
            // if something goes wrong. This way, whoever is watching
            // THIS service, if any, will see the improper shutdown
            // and recreate and restart it.
            if(File.Exists(m_processBinaryAbsPath))
            {
                bool createdNew = true;
                var mutex = new Mutex(true, string.Format(@"Global\{0}", m_processBinaryAbsPath.Replace("\\", "").Replace(" ", "")), out createdNew);

                if(createdNew)
                {
                    switch(m_isTargetService)
                    {
                        case true:
                        {
                            var uninstallStartInfo = new ProcessStartInfo(m_processBinaryAbsPath);
                            uninstallStartInfo.Arguments = "Uninstall";
                            uninstallStartInfo.UseShellExecute = false;
                            uninstallStartInfo.CreateNoWindow = true;
                            var uninstallProc = Process.Start(uninstallStartInfo);
                            if(!uninstallProc.WaitForExit(200))
                            {
                                uninstallProc.Kill();
                            }

                            var installStartInfo = new ProcessStartInfo(m_processBinaryAbsPath);
                            installStartInfo.Arguments = "Install";
                            installStartInfo.UseShellExecute = false;
                            installStartInfo.CreateNoWindow = true;

                            var installProc = Process.Start(installStartInfo);
                            if(!installProc.WaitForExit(200))
                            {
                                installProc.Kill();
                            }

                            TimeSpan timeout = TimeSpan.FromSeconds(60);

                            foreach(var service in ServiceController.GetServices())
                            {
                                if(service.ServiceName.IndexOf(Path.GetFileNameWithoutExtension(m_processBinaryAbsPath), StringComparison.OrdinalIgnoreCase) != -1)
                                {   
                                    if(service.Status == ServiceControllerStatus.StartPending)
                                    {
                                        service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                                    }

                                    if(service.Status != ServiceControllerStatus.Running)
                                    {
                                        service.Start();
                                        service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                                    }
                                }
                            }
                        }
                        break;

                        case false:
                        {
                            try
                            {
                                var startInfo = new ProcessStartInfo(m_processBinaryAbsPath);
                                startInfo.LoadUserProfile = true;
                                startInfo.UseShellExecute = false;
                                startInfo.CreateNoWindow = false;
                                startInfo.Verb = "runas";
                                Process.Start(startInfo);
                                //Process.Start(string.Format("{0}", m_processBinaryAbsPath));
                                //ProcessExecution.StartProcessAsCurrentUser(null, m_processBinaryAbsPath, null, true);
                            }
                            catch(Exception e)
                            {
                                var sb = new StringBuilder();

                                while(e != null)
                                {
                                    sb.Append(e.Message);
                                    sb.Append(e.StackTrace);
                                    e = e.InnerException;
                                }

                                File.WriteAllText(@"C:\CloudVeil.log", sb.ToString());
                            }

                        }
                        break;
                    }
                }
                else
                {
                    // Someone else is trying to save our process. Give it a second
                    // before we try to grab a new watch handle to it.
                    Task.Delay(1000).Wait();
                }

                foreach(var proc in Process.GetProcesses())
                {
                    if(proc.ProcessName.Equals(m_processToWatch, StringComparison.OrdinalIgnoreCase))
                    {   
                        SetProcessHandle(proc);
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// This will be called when the base class has determined that the function of this object
        /// is fundamentally over.
        /// </summary>
        public abstract void Shutdown(ExitCodes code);
    }
}