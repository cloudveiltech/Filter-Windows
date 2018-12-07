using System;
using System.Collections.Generic;
using System.Text;

namespace Citadel.IPC.Messages
{
    public class ConfigurationInfoMessage : ServerOnlyMessage
    {
        public List<string> SelfModeratedSites { get; set; }
        
        // TODO: Time Restrictions
    }
}
