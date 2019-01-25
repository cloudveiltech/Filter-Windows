using System;
using System.Collections.Generic;
using System.Text;

namespace Citadel.IPC.Messages
{
    [Serializable]
    [Obsolete("Use Send/Request APIs instead (IIpcCommunicator)")]
    public class RequestMessage : BaseMessage
    {
        public RequestMessage(IpcMessageBehavior behavior)
        {
            Behavior = behavior;
        }

        public IpcMessageBehavior Behavior { get; set; }

        public IpcMessageClass MessageClass { get; set; }
    }
}
