/*
* Copyright � 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using CloudVeil.Core.Windows.WinAPI;
﻿using Filter.Platform.Common.Client;
using Gui.CloudVeil.Util;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceModel.Channels;
using System.Threading;

namespace CloudVeil.Core.Windows.Client
{
    [StructLayout(LayoutKind.Sequential)]
    public struct COPYDATASTRUCT : IDisposable
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
        public void Dispose()
        {
            if (lpData != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(lpData);
                lpData = IntPtr.Zero;
                cbData = 0;
            }
        }
        public string AsAnsiString { get { return Marshal.PtrToStringAnsi(lpData, cbData); } }
        public string AsUnicodeString { get { return Marshal.PtrToStringUni(lpData); } }
        public static COPYDATASTRUCT CreateForString(int dwData, string value, bool Unicode = false)
        {
            var result = new COPYDATASTRUCT();
            result.dwData = (IntPtr)dwData;
            result.lpData = Unicode ? Marshal.StringToCoTaskMemUni(value) : Marshal.StringToCoTaskMemAnsi(value);
            result.cbData = (Unicode ? 2 : 1)*value.Length + 1;
            return result;
        }
    }

    public class WindowsGUIChecks : IGUIChecks
    {
        public void DisplayExistingUI(string url)
        {
            var thisProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(thisProcess.ProcessName).Where(p => p.Id != thisProcess.Id);

            foreach (Process runningProcess in processes)
            {
                foreach (var handle in WindowHelpers.EnumerateProcessWindowHandles(runningProcess.Id))
                {
                    if(url.Length > 0) {
                        var data = COPYDATASTRUCT.CreateForString(1, url, true);
                        WindowHelpers.SendMessage(handle, (int)WindowMessages.WM_COPY_DATA, IntPtr.Zero, ref data);
                    }
                    else
                    {
                        WindowHelpers.SendMessage(handle, (int)WindowMessages.CV_SHOW_WINDOW, 0, 0);
                    }
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
            catch (Exception)
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
