/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using GalaSoft.MvvmLight;
using NLog;
using System;
using System.Threading.Tasks;
using Te.Citadel.Util;

namespace Te.Citadel.UI.Models
{
    internal class DashboardModel : ObservableObject
    {
        public delegate void RelaxedPolicyRequestDelegate();

        public event RelaxedPolicyRequestDelegate RelaxedPolicyRequested;

        public event RelaxedPolicyRequestDelegate RelinquishRelaxedPolicyRequested;

        private readonly Logger m_logger;

        private int m_availableRelaxedRequests = 0;

        private string m_relaxedDuration = "0";

        public DashboardModel()
        {
            m_logger = LoggerUtil.GetAppWideLogger();
        }

        public async Task<bool> RequestAppDeactivation()
        {
            try
            {
                var response = await WebServiceUtil.RequestResource(WebServiceUtil.ServiceResource.DeactivationRequest);

                // WebServiceUtil.RequestResource gives null on failure, non-null on success of any
                // kind.
                return response != null;
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }

            return false;
        }

        public void RequestRelaxedPolicy()
        {
            RelaxedPolicyRequested?.Invoke();
        }

        public void RelinquishRelaxedPolicy()
        {
            RelinquishRelaxedPolicyRequested?.Invoke();
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