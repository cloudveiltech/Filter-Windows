/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using CloudVeil.Core.Windows.Util;
using Filter.Platform.Common;
using Filter.Platform.Common.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeil.Core.Windows
{
    public class WindowsFilterAgent : IFilterAgent
    {
        private int agentProcess(string arguments, bool runasAdmin = true)
        {
            try
            {
                ProcessStartInfo startupInfo = new ProcessStartInfo();
                startupInfo.FileName = "FilterAgent.Windows.exe";
                startupInfo.Arguments = arguments;
                startupInfo.WindowStyle = ProcessWindowStyle.Hidden;

                if (runasAdmin)
                {
                    startupInfo.Verb = "runas";
                }

                startupInfo.CreateNoWindow = true;
                Process process = Process.Start(startupInfo);

                process.WaitForExit();

                return process.ExitCode;
            }
            catch(Exception ex)
            {
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), ex);
            }

            return -1;
        }

        public void StartFilter()
        {
            bool mainServiceViable = true;
            try
            {
                var sc = new ServiceController("FilterServiceProvider");

                switch(sc.Status)
                {
                    case ServiceControllerStatus.Stopped:
                    case ServiceControllerStatus.StopPending:
                        mainServiceViable = false;
                        break;
                }
            }
            catch(Exception)
            {
                mainServiceViable = false;
            }

            if(!mainServiceViable)
            {
                agentProcess("start", true);
            }
        }

        public ConnectivityCheck.Accessible CheckConnectivity()
        {
            return (ConnectivityCheck.Accessible)agentProcess("check", false);
        }
    }
}
