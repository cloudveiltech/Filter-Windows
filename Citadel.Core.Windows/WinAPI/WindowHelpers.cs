/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Citadel.Core.Windows.WinAPI
{
    public static class WindowHelpers
    {
        public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn,
            IntPtr lParam);

        /// <summary>
        /// This callback is used by the EnumerateProcessWindowHandles process, which is supplied to
        /// the WinAPI EnumThreadWindows method.
        /// </summary>
        /// <param name="hWndm">
        /// Window handle. 
        /// </param>
        /// <param name="lParam">
        /// Param. In this case, our param is a list container we store all windows in. 
        /// </param>
        /// <returns>
        /// Always true, to keep enumerating. 
        /// </returns>
        private static bool OnEnumThread(IntPtr hWndm, IntPtr lParam)
        {
            IList<IntPtr> handles = GCHandle.FromIntPtr(lParam).Target as List<IntPtr>;

            if(handles != null)
            {
                handles.Add(hWndm);
            }

            return true;
        }

        /// <summary>
        /// Gets a list of all window handles for all threads belonging to process identified by the
        /// supplied process ID.
        /// </summary>
        /// <param name="processId">
        /// The ID of the process to target. 
        /// </param>
        /// <returns>
        /// A list of all discovered window handles. 
        /// </returns>
        public static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            var listHandle = GCHandle.Alloc(handles);

            foreach(ProcessThread thread in Process.GetProcessById(processId).Threads)
            {
                EnumThreadWindows(thread.Id, OnEnumThread, (IntPtr)listHandle);
            }

            listHandle.Free();

            return handles;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool SendMessage(IntPtr hWnd, uint Msg, bool wParam, long lParam);

        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Ansi)]
        public static extern bool SendMessage(IntPtr hwnd, uint wMsg, uint wParam, long lParam);
    }
}