/*
* Copyright � 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
﻿using Filter.Platform.Common.Client;
using System;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using Gui.CloudVeil.Util;
using CloudVeil.Core.Windows.WinAPI;

namespace CloudVeil.Core.Windows.Client
{
    public class WindowsGUIChecks : IGUIChecks
    {
        public void DisplayExistingUI()
        {
            var thisProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(thisProcess.ProcessName).Where(p => p.Id != thisProcess.Id);

            foreach (Process runningProcess in processes)
            {
                foreach (var handle in WindowHelpers.EnumerateProcessWindowHandles(runningProcess.Id))
                {
                    // Send window show.
                    WindowHelpers.SendMessage(handle, (uint)WindowMessages.SHOWWINDOW, 9, 0);
                }
            }
        }

        public bool IsAlreadyRunning()
        {
            string appVerStr = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            appVerStr += "." + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString(3);

            bool createdNew;
            try
            {
                var instanceMutex = new Mutex(true, $"Local\\{GuidUtility.Create(GuidUtility.DnsNamespace, appVerStr).ToString("B")}", out createdNew);
                instanceMutex.ReleaseMutex();
            }
            catch (Exception)
            {
                // We can get access denied if SYSTEM is running this.
                createdNew = false;
            }

            return createdNew;
        }

        public bool IsInIsolatedSession()
        {
            return Process.GetCurrentProcess().SessionId <= 0;
        }

        Mutex instanceMutex;

        public bool PublishRunningApp()
        {
            string appVerStr = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            appVerStr += "." + System.Reflection.AssemblyName.GetAssemblyName(assembly.Location).Version.ToString(3);

            bool createdNew;
            try
            {
                instanceMutex = new Mutex(true, $"Local\\{GuidUtility.Create(GuidUtility.DnsNamespace, appVerStr).ToString("B")}", out createdNew);
            }
            catch (Exception ex)
            {
                // We can get access denied if SYSTEM is running this.
                createdNew = false;
            }

            return createdNew;
        }

        public void UnpublishRunningApp()
        {
            if (instanceMutex != null)
            {
                instanceMutex.ReleaseMutex();
            }
        }
    }
}
