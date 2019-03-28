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
using Filter.Platform.Common.Util;
using Filter.Platform.Common;
using System.IO;

namespace Filter.Platform.Mac
{
    public class MacGUIChecks : IGUIChecks
    {
        [DllImport(Platform.NativeLib)]
        private static extern bool IsEffectiveUserIdRoot();

        private NLog.Logger logger;
        private IPathProvider paths;

        public MacGUIChecks()
        {
            logger = LoggerUtil.GetAppWideLogger();
            paths = PlatformTypes.New<IPathProvider>();
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

        public bool IsAlreadyRunning()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cloudveil");

            if(!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string lockFile = Path.Combine(dir, ".cloudveil.lock");

            if(Platform.IsFileLocked(lockFile))
            {
                return true;
            }

            return false;
        }

        private int lockFd = -1;
        public bool PublishRunningApp()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cloudveil");

            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string lockFile = Path.Combine(dir, ".cloudveil.lock");

            if(Platform.IsFileLocked(lockFile))
            {
                return false;
            }

            int fd = 0;
            if(Platform.AcquireFileLock(lockFile, out fd))
            {
                lockFd = fd;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void UnpublishRunningApp()
        {
            if(lockFd >= 0)
            {
                Platform.ReleaseFileLock(lockFd);
            }
        }
    }
}
