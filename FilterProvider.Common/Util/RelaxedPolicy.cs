using Citadel.IPC;
using Citadel.IPC.Messages;
using Filter.Platform.Common.Data.Models;
using Filter.Platform.Common.Util;
using FilterProvider.Common.Configuration;
using FilterProvider.Common.Services;
using GoProxyWrapper;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace FilterProvider.Common.Util
{
    /// <summary>
    /// object for JSON response for /api/relaxedpolicy on local server.
    /// </summary>
    class BypassInformation
    {
        internal int BypassesPermitted { get; set; }
        internal int BypassesUsed { get; set; }
    }

    public class RelaxedPolicy
    {
        private NLog.Logger logger;
        private IPCServer ipcServer;
        private IPolicyConfiguration policyConfiguration;
        /// <summary>
        /// This timer is used to count down to the expiry time for relaxed policy use. 
        /// </summary>
        private Timer m_relaxedPolicyExpiryTimer;

        /// <summary>
        /// This timer is used to track a 24 hour cooldown period after the exhaustion of all
        /// available relaxed policy uses. Once the timer is expired, it will reset the count to the
        /// config default and then disable itself.
        /// </summary>
        private Timer m_relaxedPolicyResetTimer;

        public RelaxedPolicy(IPCServer server, IPolicyConfiguration configuration)
        {
            logger = LoggerUtil.GetAppWideLogger();
            ipcServer = server;
            policyConfiguration = configuration;

            policyConfiguration.OnConfigurationLoaded += OnConfigLoaded_LoadRelaxedPolicy;
            policyConfiguration.OnConfigurationLoaded += (sender, e) => SendRelaxedPolicyInfo();
        }

        /// <summary>
        /// Sends the relaxed policy information to all clients.
        /// </summary>
        public void SendRelaxedPolicyInfo()
        {
            var cfg = policyConfiguration.Configuration;

            if (cfg != null && cfg.BypassesPermitted > 0)
            {
                ipcServer.NotifyRelaxedPolicyChange(cfg.BypassesPermitted - cfg.BypassesUsed, cfg.BypassDuration, getRelaxedPolicyStatus());
            }
            else
            {
                ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero, getRelaxedPolicyStatus());
            }
        }

        public class RelaxedPolicyResponseObject
        {
            public bool allowed { get; set; }
            public string message { get; set; }
            public int used { get; set; }
            public int permitted { get; set; }
        }

        internal BypassInformation GetInfo()
        {
            var config = policyConfiguration.Configuration;

            BypassInformation info = new BypassInformation()
            {
                BypassesPermitted = 0,
                BypassesUsed = 0
            };

            if (config != null)
            {
                info.BypassesPermitted = config.BypassesPermitted;
                info.BypassesUsed = config.BypassesUsed;
            }

            return info;
        }

        /// <summary>
        /// Whenever the config is reloaded, sync the bypasses from the server.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnConfigLoaded_LoadRelaxedPolicy(object sender, EventArgs e)
        {
            try
            {
                this.UpdateNumberOfBypassesFromServer();
            }
            catch (Exception ex)
            {
                // TODO: Tell Sentry about this.
                LoggerUtil.RecursivelyLogException(logger, ex);
            }
        }

        private void enableRelaxedPolicy()
        {
            logger.Info("enableRelaxedPolicy");
            AdBlockMatcherApi.EnableBypass();
        }

        private void disableRelaxedPolicy()
        {
            logger.Info("disableRelaxedPolicy");
            AdBlockMatcherApi.DisableBypass();
        }

        public bool RequestRelaxedPolicy(string passcode, out string bypassNotification)
        {
            HttpStatusCode statusCode;
            bool grantBypass = false;

            if(getRelaxedPolicyStatus() == RelaxedPolicyStatus.Activated)
            {
                bypassNotification = "Relaxed Policy already in effect.";

                var cfg = policyConfiguration.Configuration;
                if(cfg != null)
                {
                    ipcServer.NotifyRelaxedPolicyChange(cfg.BypassesPermitted - cfg.BypassesUsed, cfg.BypassDuration, RelaxedPolicyStatus.Activated, bypassNotification);
                }
                else
                {
                    ipcServer.NotifyRelaxedPolicyChange(0, new TimeSpan(0), RelaxedPolicyStatus.Activated, bypassNotification);
                }

                return false;
            }

            var parameters = new Dictionary<string, object>();
            if (passcode != null)
            {
                parameters.Add("passcode", passcode);
            }

            byte[] bypassResponse = WebServiceUtil.Default.RequestResource(ServiceResource.BypassRequest, out statusCode, parameters);

            bool useLocalBypassLogic = false;

            bypassNotification = "";

            int bypassesUsed = 0;
            int bypassesPermitted = 0;

            if (bypassResponse != null)
            {
                if (statusCode == HttpStatusCode.NotFound)
                {
                    // Fallback on local bypass logic if server does not support relaxed policy checks.
                    useLocalBypassLogic = true;
                }

                string jsonString = Encoding.UTF8.GetString(bypassResponse);
                logger.Info("Response received {0}: {1}", statusCode.ToString(), jsonString);

                var bypassObject = JsonConvert.DeserializeObject<RelaxedPolicyResponseObject>(jsonString);

                if (bypassObject.allowed)
                {
                    grantBypass = true;
                }
                else
                {
                    grantBypass = false;
                    bypassNotification = bypassObject.message;
                }

                bypassesUsed = bypassObject.used;
                bypassesPermitted = bypassObject.permitted;
            }
            else
            {
                logger.Info("No response detected.");

                useLocalBypassLogic = false;
                grantBypass = false;
            }

            if (useLocalBypassLogic)
            {
                logger.Info("Using local bypass logic since server does not yet support bypasses.");

                // Start the count down timer.
                if (m_relaxedPolicyExpiryTimer == null)
                {
                    m_relaxedPolicyExpiryTimer = new Timer(OnRelaxedPolicyTimerExpired, null, Timeout.Infinite, Timeout.Infinite);
                }

                enableRelaxedPolicy();

                var cfg = policyConfiguration.Configuration;
                m_relaxedPolicyExpiryTimer.Change(cfg != null ? cfg.BypassDuration : TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);

                DecrementRelaxedPolicy_Local();
            }
            else
            {
                if (grantBypass)
                {
                    logger.Info("Relaxed policy granted.");

                    // Start the count down timer.
                    if (m_relaxedPolicyExpiryTimer == null)
                    {
                        m_relaxedPolicyExpiryTimer = new Timer(OnRelaxedPolicyTimerExpired, null, Timeout.Infinite, Timeout.Infinite);
                    }

                    enableRelaxedPolicy();
                    

                    var cfg = policyConfiguration.Configuration;
                    m_relaxedPolicyExpiryTimer.Change(cfg != null ? cfg.BypassDuration : TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);

                    cfg.BypassesUsed = bypassesUsed;
                    cfg.BypassesPermitted = bypassesPermitted;

                    DecrementRelaxedPolicy(bypassesUsed, bypassesPermitted, cfg != null ? cfg.BypassDuration : TimeSpan.FromMinutes(5));
                }
                else
                {
                    var cfg = policyConfiguration.Configuration;

                    RelaxedPolicyStatus status = RelaxedPolicyStatus.AllUsed;
                    if (bypassNotification.IndexOf("incorrect passcode", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        status = RelaxedPolicyStatus.Unauthorized;
                    }

                    ipcServer.NotifyRelaxedPolicyChange(bypassesPermitted - bypassesUsed, cfg != null ? cfg.BypassDuration : TimeSpan.FromMinutes(5), status);
                }
            }

            return grantBypass;
        }

        /// <summary>
        /// Called whenever a relaxed policy has been requested. 
        /// </summary>
        public void OnRelaxedPolicyRequested(RelaxedPolicyEventArgs args)
        {
            if(args.Command == RelaxedPolicyCommand.Relinquished)
            {
                OnRelinquishRelaxedPolicyRequested();
                return;
            }

            string bypassNotification = null;
            RequestRelaxedPolicy(args.Passcode, out bypassNotification);
        }

        private void DecrementRelaxedPolicy(int bypassesUsed, int bypassesPermitted, TimeSpan bypassDuration)
        {
            bool allUsesExhausted = (bypassesUsed >= bypassesPermitted);

            if (allUsesExhausted)
            {
                logger.Info("All uses exhausted.");

                ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero, RelaxedPolicyStatus.AllUsed);
            }
            else
            {
                ipcServer.NotifyRelaxedPolicyChange(bypassesPermitted - bypassesUsed, bypassDuration, RelaxedPolicyStatus.Granted);
            }

            if (allUsesExhausted)
            {
                // Reset our bypasses at 8:15 UTC.
                var resetTime = DateTime.UtcNow.Date.AddHours(8).AddMinutes(15);

                var span = resetTime - DateTime.UtcNow;

                if (m_relaxedPolicyResetTimer == null)
                {
                    m_relaxedPolicyResetTimer = new Timer(OnRelaxedPolicyResetExpired, null, span, Timeout.InfiniteTimeSpan);
                }

                m_relaxedPolicyResetTimer.Change(span, Timeout.InfiniteTimeSpan);
            }
        }

        private void DecrementRelaxedPolicy_Local()
        {
            bool allUsesExhausted = false;

            var cfg = policyConfiguration.Configuration;

            if (cfg != null)
            {
                cfg.BypassesUsed++;

                allUsesExhausted = cfg.BypassesPermitted - cfg.BypassesUsed <= 0;

                if (allUsesExhausted)
                {
                    ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero, RelaxedPolicyStatus.AllUsed);
                }
                else
                {
                    ipcServer.NotifyRelaxedPolicyChange(cfg.BypassesPermitted - cfg.BypassesUsed, cfg.BypassDuration, RelaxedPolicyStatus.Granted);
                }
            }
            else
            {
                ipcServer.NotifyRelaxedPolicyChange(0, TimeSpan.Zero, RelaxedPolicyStatus.Granted);
            }

            if (allUsesExhausted)
            {
                // Refresh tomorrow at midnight.
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var span = tomorrow - DateTime.Now;

                if (m_relaxedPolicyResetTimer == null)
                {
                    m_relaxedPolicyResetTimer = new Timer(OnRelaxedPolicyResetExpired, null, span, Timeout.InfiniteTimeSpan);
                }

                m_relaxedPolicyResetTimer.Change(span, Timeout.InfiniteTimeSpan);
            }
        }

        private RelaxedPolicyStatus getRelaxedPolicyStatus()
        {
            bool relaxedInEffect = AdBlockMatcherApi.GetBypassEnabled();

            if (relaxedInEffect)
            {
                return RelaxedPolicyStatus.Activated;
            }
            else
            {
                if (policyConfiguration.Configuration != null && policyConfiguration.Configuration.BypassesPermitted - policyConfiguration.Configuration.BypassesUsed == 0)
                {
                    return RelaxedPolicyStatus.AllUsed;
                }
                else
                {
                    return RelaxedPolicyStatus.Deactivated;
                }
            }
        }

        /// <summary>
        /// Called when the user has manually requested to relinquish a relaxed policy. 
        /// </summary>
        private void OnRelinquishRelaxedPolicyRequested()
        {
            RelaxedPolicyStatus status = getRelaxedPolicyStatus();

            // Ensure timer is stopped and re-enable categories by simply calling the timer's expiry callback.
            if (status == RelaxedPolicyStatus.Activated)
            {
                var cfg = policyConfiguration.Configuration;

                OnRelaxedPolicyTimerExpired(null);
            }

            // We want to inform the user that there is no relaxed policy in effect currently for this installation.
            if (status == RelaxedPolicyStatus.Deactivated)
            {
                var cfg = policyConfiguration.Configuration;
                ipcServer.NotifyRelaxedPolicyChange(cfg.BypassesPermitted - cfg.BypassesUsed, cfg.BypassDuration, RelaxedPolicyStatus.AlreadyRelinquished);
            }
        }

        /// <summary>
        /// Called whenever the relaxed policy duration has expired. 
        /// </summary>
        /// <param name="state">
        /// Async state. Not used. 
        /// </param>
        private void OnRelaxedPolicyTimerExpired(object state)
        {
            try
            {
                var cfg = policyConfiguration.Configuration;

                disableRelaxedPolicy();

                ipcServer.NotifyRelaxedPolicyChange(cfg.BypassesPermitted - cfg.BypassesUsed, cfg.BypassDuration, RelaxedPolicyStatus.Deactivated);

                // Disable the expiry timer.
                m_relaxedPolicyExpiryTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
            }
        }

        private bool UpdateNumberOfBypassesFromServer()
        {
            HttpStatusCode statusCode;
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters.Add("check_only", "1");

            byte[] response = WebServiceUtil.Default.RequestResource(ServiceResource.BypassRequest, out statusCode, parameters);

            if (response == null)
            {
                return false;
            }

            string responseString = Encoding.UTF8.GetString(response);

            var bypassInfo = JsonConvert.DeserializeObject<RelaxedPolicyResponseObject>(responseString);

            logger.Info("Bypass info: {0}/{1}", bypassInfo.used, bypassInfo.permitted);
            var cfg = policyConfiguration.Configuration;

            if (cfg != null)
            {
                cfg.BypassesUsed = bypassInfo.used;
                cfg.BypassesPermitted = bypassInfo.permitted;
            }

            ipcServer.NotifyRelaxedPolicyChange(bypassInfo.permitted - bypassInfo.used, cfg != null ? cfg.BypassDuration : TimeSpan.FromMinutes(5), getRelaxedPolicyStatus());
            return true;
        }

        /// <summary>
        /// Called whenever the relaxed policy reset timer has expired. This expiry refreshes the
        /// available relaxed policy requests to the configured value.
        /// </summary>
        /// <param name="state">
        /// Async state. Not used. 
        /// </param>
        private void OnRelaxedPolicyResetExpired(object state)
        {
            try
            {
                UpdateNumberOfBypassesFromServer();
                // Disable the reset timer.
                m_relaxedPolicyResetTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                // TODO: Tell sentry about this.
                LoggerUtil.RecursivelyLogException(logger, ex);
            }
        }

    }
}
