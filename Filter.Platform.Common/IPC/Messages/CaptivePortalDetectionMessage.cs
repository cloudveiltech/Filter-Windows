using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Citadel.IPC.Messages
{
    /// <summary>
    /// Used by server to send detected state of captive portal.
    /// Used by client to send a request for captive portal detection.
    /// </summary>
    [Serializable]
    public class CaptivePortalDetectionMessage : BaseMessage
    {
        public CaptivePortalDetectionMessage(bool isCaptivePortalDetected, bool isCaptivePortalActive)
        {
            IsCaptivePortalDetected = isCaptivePortalDetected;
        }

        /// <summary>
        /// This is true whenever the filter thinks we're on a captive portal network.
        /// </summary>
        public bool IsCaptivePortalDetected { get; set; }

        /// <summary>
        /// This is true when the captive portal page is actively blocking us.
        /// </summary>
        public bool IsCaptivePortalActive { get; set; }
    }
}
