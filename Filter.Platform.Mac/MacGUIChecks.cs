// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using AppKit;
using System.Runtime.InteropServices;
using Filter.Platform.Common.Client;
using System.Diagnostics;
using System.Linq;

namespace Filter.Platform.Mac
{
    public class MacGUIChecks : IGUIChecks
    {
        [DllImport(Platform.NativeLib)]
        private static extern bool IsEffectiveUserIdRoot();

        public MacGUIChecks()
        {
        }

        public void DisplayExistingUI()
        {
            var thisProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(thisProcess.ProcessName).Where(p => p.Id != thisProcess.Id);

            foreach (Process runningProcess in processes)
            {
                NSRunningApplication app = NSRunningApplication.GetRunningApplication(runningProcess.Id);
                app.Activate(NSApplicationActivationOptions.ActivateAllWindows);
            }
        }

        public bool IsInIsolatedSession()
        {
            return IsEffectiveUserIdRoot();
        }
    }
}
