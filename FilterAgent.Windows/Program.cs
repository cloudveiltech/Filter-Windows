/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Security.Principal;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Filter.Platform.Common.Util;
using System.ServiceProcess;
using System.Net;

namespace FilterAgent.Windows
{
    /// <summary>
    /// This little utility fills a simple void. We need an agent utility for elevation and allowing us to start
    /// the filter without elevating the GUI.
    /// </summary>
    class Program
    {
        private static string s_mutexName;
        private static string s_processBinaryAbsPath;

        private static readonly HashSet<char> s_toRemoveFromPath = new HashSet<char>
        {
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
            Path.PathSeparator,
            ':'
        };

        private static bool hasElevatedPrivileges()
        {
            var id = WindowsIdentity.GetCurrent();

            var principal = new WindowsPrincipal(id);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static Process getRunningProcess(string processNameToObserve)
        {
            foreach(var proc in Process.GetProcesses())
            {
                if(proc.ProcessName.Equals(processNameToObserve, StringComparison.OrdinalIgnoreCase) && !proc.HasExited)
                {
                    return proc;
                }
            }

            return null;
        }

        private static void startService(string serviceName)
        {
            int numTries = 0;
            while(!resuscitate(serviceName) && numTries < 60)
            {
                Thread.Sleep(1000);
                ++numTries;
            }

            if(numTries >= 60)
            {
                Environment.Exit((int)ExitCodes.ShutdownCriticalError);
            }
        }

        private static bool resuscitate(string serviceName)
        {
            Console.WriteLine("Attempting start of service {0}", serviceName);

            bool success = false;

            bool createdNew = true;
            var mutex = new Mutex(true, string.Format(@"Global\{0}", s_mutexName), out createdNew);

            try
            {
                if(File.Exists(s_processBinaryAbsPath))
                {
                    if(createdNew)
                    {
                        var uninstallStartInfo = new ProcessStartInfo(s_processBinaryAbsPath);
                        uninstallStartInfo.Arguments = "Uninstall";
                        uninstallStartInfo.UseShellExecute = false;
                        uninstallStartInfo.CreateNoWindow = true;
                        var uninstallProc = Process.Start(uninstallStartInfo);
                        uninstallProc.WaitForExit();

                        var installStartInfo = new ProcessStartInfo(s_processBinaryAbsPath);
                        installStartInfo.Arguments = "Install";
                        installStartInfo.UseShellExecute = false;
                        installStartInfo.CreateNoWindow = true;

                        var installProc = Process.Start(installStartInfo);
                        installProc.WaitForExit();

                        Console.WriteLine("Filter service installed");

                        TimeSpan timeout = TimeSpan.FromSeconds(60);

                        foreach(var service in ServiceController.GetServices())
                        {
                            if(service.ServiceName.IndexOf(Path.GetFileNameWithoutExtension(s_processBinaryAbsPath), StringComparison.OrdinalIgnoreCase) != -1)
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
                    else
                    {
                        Console.WriteLine("Service seems to be running already.");
                    }
                } else
                {
                    Console.WriteLine("File does not exist '{0}'", s_processBinaryAbsPath);
                }
            }
            catch
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

            return success;
        }

        static void usage()
        {
            Console.WriteLine("Usage: FilterAgent.Windows.exe [start|check]");
        }

        static int connectivityCheck()
        {
            return (int)ConnectivityCheck.IsAccessible();
        }

        static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                usage();
                Environment.Exit(1);
            }

            if(!hasElevatedPrivileges())
            {
                Console.WriteLine("Requires elevated privileges");
                Environment.Exit(2);
            }

            if(args[0] == "start")
            {
                if(!hasElevatedPrivileges())
                {
                    Console.WriteLine("Requires elevated privileges");
                    Environment.Exit(2);
                }
                // Find FilterServiceProvider.exe
                if (!File.Exists("FilterServiceProvider.exe"))
                {
                    Console.WriteLine("Couldn't find FilterServiceProvider.exe");
                    Environment.Exit(3);
                }

                Console.WriteLine("FilterAgent.Windows all systems go");

                // FilterServiceProvider.exe exists, so start it.
                string processName = "FilterServiceProvider";
                string baseDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(Program)).Location);
                string processBinaryPath = Path.Combine(baseDirectory, $"{processName}.exe");

                s_mutexName = string.Join("", processBinaryPath.Where(x => !s_toRemoveFromPath.Contains(x)).ToList());
                s_processBinaryAbsPath = processBinaryPath;

                if (getRunningProcess("FilterServiceProvider") != null)
                {
                    Environment.Exit(0);
                }

                startService("FilterServiceProvider");
            }
            else if(args[0] == "check")
            {
                Environment.Exit(connectivityCheck());
            }
            else
            {
                usage();
                Environment.Exit(1);
            }
        }
    }
}
