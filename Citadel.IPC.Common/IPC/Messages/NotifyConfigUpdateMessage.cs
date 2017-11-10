using Citadel.Core.Windows.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Citadel.IPC.Messages
{
    [Serializable]
    public class NotifyConfigUpdateMessage : BaseMessage
    {
        public ConfigUpdateResult UpdateResult { get; set; }

        public NotifyConfigUpdateMessage(ConfigUpdateResult result)
        {
            UpdateResult = result;
        }
    }
}
