/*
* Copyright © 2017 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Util;
using Citadel.IPC;
using Filter.Platform.Common.Util;
using GalaSoft.MvvmLight;
using NLog;
using System;
using System.Threading.Tasks;
using Te.Citadel.Util;

namespace Te.Citadel.UI.Models
{
    public class DashboardModel : ObservableObject
    {
        public delegate void RelaxedPolicyRequestDelegate();

        

        private readonly Logger m_logger;

        private int m_availableRelaxedRequests = 0;

        private string m_relaxedDuration = "0";

        private DateTime m_lastSync = DateTime.Now;

        public DashboardModel()
        {
            m_logger = LoggerUtil.GetAppWideLogger();
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
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }

            return false;
        }

        public int AvailableRelaxedRequests
        {
            get
            {
                return m_availableRelaxedRequests;
            }

            set
            {
                m_availableRelaxedRequests = value;
            }
        }

        public string RelaxedDuration
        {
            get
            {
                return m_relaxedDuration;
            }

            set
            {
                m_relaxedDuration = value;
            }
        }

    }
}