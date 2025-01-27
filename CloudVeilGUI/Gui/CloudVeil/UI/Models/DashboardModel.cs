/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeil.Core.Windows.Util;
using CloudVeil.IPC;
using Filter.Platform.Common.Util;
using GalaSoft.MvvmLight;
using NLog;
using System;
using System.Threading.Tasks;
using Gui.CloudVeil.Util;

namespace Gui.CloudVeil.UI.Models
{
    public class DashboardModel : ObservableObject
    {
        public delegate void RelaxedPolicyRequestDelegate();

        

        private readonly Logger logger;

        private int availableRelaxedRequests = 0;

        private string relaxedDuration = "0";

        private DateTime lastSync = DateTime.Now;

        public DashboardModel()
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        public async Task<bool> RequestAppDeactivation()
        {
            try
            {
                using(var ipcClient = new IPCClient())
                {
                    ipcClient.ConnectedToServer = () =>
                    {
                        ipcClient.RequestDeactivation();
                    };

                    ipcClient.WaitForConnection();
                    await Task.Delay(3000);
                }
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(logger, e);
            }

            return false;
        }

        public int AvailableRelaxedRequests
        {
            get
            {
                return availableRelaxedRequests;
            }

            set
            {
                availableRelaxedRequests = value;
            }
        }

        public string RelaxedDuration
        {
            get
            {
                return relaxedDuration;
            }

            set
            {
                relaxedDuration = value;
            }
        }

    }
}