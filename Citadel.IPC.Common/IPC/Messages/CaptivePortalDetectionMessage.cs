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
        public CaptivePortalDetectionMessage(bool isCaptivePortalDetected)
        {
            IsCaptivePortalDetected = isCaptivePortalDetected;
        }

        public bool IsCaptivePortalDetected { get; set; }
    }
}
