/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.Core.Windows.Util;
using CloudVeilGUI.Platform.Common;
using Filter.Platform.Common.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeil.Windows.Platform
{
    public class WindowsFilterStarter : IFilterStarter
    {
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
            catch(Exception ex)
            {
                mainServiceViable = false;
            }

            if(!mainServiceViable)
            {
                try
                {
                    ProcessStartInfo startupInfo = new ProcessStartInfo();
                    startupInfo.FileName = "FilterAgent.Windows.exe";
                    startupInfo.Arguments = "start";
                    startupInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    startupInfo.Verb = "runas";
                    startupInfo.CreateNoWindow = true;
                    Process.Start(startupInfo);
                }
                catch(Exception ex)
                {
                    LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), ex);
                }
            }
        }
    }
}
