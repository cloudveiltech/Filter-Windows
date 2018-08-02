/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        private string m_mutexName = string.Empty;

        private static readonly HashSet<char> s_toRemoveFromPath = new HashSet<char>
        {
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
            Path.PathSeparator,
            ':'
        };

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
        /// <param name="ensureRunning">
        /// Set to false if you don't want the constructor to ensure the service is already running.
        /// I added this parameter so that we could make behavior of the FilterStarter more explicit.
        /// </param>
        public BaseProtectiveService(string processNameToObserve, bool isService, bool ensureRunning = true)
        {
            m_processToWatch = processNameToObserve;
            m_baseDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(BaseProtectiveService)).Location);
            m_processBinaryAbsPath = Path.Combine(m_baseDirectory, string.Format("{0}.exe", processNameToObserve));

            m_mutexName = string.Join("", m_processBinaryAbsPath.Where(x => !s_toRemoveFromPath.Contains(x)).ToList());

            m_isTargetService = isService;

            if (ensureRunning)
            {
                EnsureAlreadyRunning();
            }
        }

        public void EnsureAlreadyRunning()
        {
            Console.WriteLine($"EnsureAlreadyRunning {m_processToWatch}");

            foreach(var proc in Process.GetProcesses())
            {
                if(proc.ProcessName.Equals(m_processToWatch, StringComparison.OrdinalIgnoreCase) && !proc.HasExited)
                {
                    // Found the process already alive. Return and do nothing.
                    Console.WriteLine($"Process was alive {proc.HasExited}");

                    SetProcessHandle(proc);
                    return;
                }
            }

            // Didn't find the process alive. Start it.
            InfinitelyStartAwaitTarget();
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

            try
            {
                m_processHandle.EnableRaisingEvents = true;
                m_processHandle.Exited += OnProcExit;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error occurred while enabling event raising. {ex}");
            }
        }

        private async void InfinitelyStartAwaitTarget()
        {
            int numTries = 0;
            while(Resuscitate() == false && numTries < 60)
            {
                await Task.Delay(1000);
                ++numTries;
            }

            if(numTries >= 60)
            {
                Shutdown(ExitCodes.ShutdownCriticalError);
            }
        }

        private void OnProcExit(object sender, EventArgs e)
        {
            var exitCode = -1;
            if(m_processHandle != null)
            {
                exitCode = m_processHandle.ExitCode;
            }

            if(exitCode < (int)ExitCodes.ShutdownWithSafeguards)
            {
                InfinitelyStartAwaitTarget();
            }
            else
            {
                Shutdown((ExitCodes)exitCode);
            }
        }

        protected virtual bool Resuscitate()
        {
            bool success = false;

            bool createdNew = true;
            var mutex = new Mutex(true, string.Format(@"Global\{0}", m_mutexName), out createdNew);

            try
            {
                if(File.Exists(m_processBinaryAbsPath))
                {
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
                                uninstallProc.WaitForExit();

                                var installStartInfo = new ProcessStartInfo(m_processBinaryAbsPath);
                                installStartInfo.Arguments = "Install";
                                installStartInfo.UseShellExecute = false;
                                installStartInfo.CreateNoWindow = true;

                                var installProc = Process.Start(installStartInfo);
                                installProc.WaitForExit();

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

                                success = true;
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

                                    return true;
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

                                    success = false;
                                }
                            }
                            break;
                        }
                    }
                    else
                    {
                        // Someone else is trying to save our process.
                        success = false;
                    }
                }
            }
            catch(Exception e)
            {
                success = false;
            }

            if(mutex != null)
            {
                if(createdNew)
                {
                    mutex.ReleaseMutex();
                }

                mutex.Dispose();
            }

            if(success == true)
            {
                foreach(var proc in Process.GetProcesses())
                {
                    if(proc.ProcessName.Equals(m_processToWatch, StringComparison.OrdinalIgnoreCase))
                    {
                        SetProcessHandle(proc);
                        break;
                    }
                }
            }

            return success;
        }

        /// <summary>
        /// This will be called when the base class has determined that the function of this object
        /// is fundamentally over.
        /// </summary>
        public abstract void Shutdown(ExitCodes code);
    }
}