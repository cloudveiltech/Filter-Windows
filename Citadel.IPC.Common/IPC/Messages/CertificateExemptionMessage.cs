using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Citadel.IPC.Messages
{
    [Serializable]
    public class CertificateExemptionMessage : BaseMessage
    {
        public string Host { get; set; }
        public string CertificateHash { get; set; }
        public bool ExemptionGranted { get; set; }

        public CertificateExemptionMessage(string host, string certHash, bool exemptionGranted)
        {
            Host = host;
            CertificateHash = certHash;
            ExemptionGranted = exemptionGranted;
        }
    }
}
