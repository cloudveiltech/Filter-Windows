using GalaSoft.MvvmLight;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
            m_logger = LogManager.GetLogger("Citadel");
        }

        public async Task<bool> RequestAppDeactivation()
        {
            try
            {               
                var deactivationRouteStr = "/capi/deactivate.php"; 

                var response = await WebServiceUtil.RequestResource(deactivationRouteStr);

                // WebServiceUtil.RequestResource gives null on failure, non-null on success of any kind.
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
