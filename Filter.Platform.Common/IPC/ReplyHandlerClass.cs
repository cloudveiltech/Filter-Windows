using Citadel.IPC.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Citadel.IPC
{
    public delegate bool OnReplyHandler(ReplyHandlerClass h, IpcMessage message);

    public class ReplyHandlerClass
    {
        public ReplyHandlerClass(IIpcCommunicator comm)
        {
            communicator = comm;
        }

        private IIpcCommunicator communicator;

        public event OnReplyHandler Handler;

        public bool TriggerHandler(BaseMessage msg)
        {
            if(msg is IpcMessage)
            {
                return Handler?.Invoke(this, msg) ?? false;
            }
            else
            {
                return false;
            }
        }

        public void OnReply(OnReplyHandler callback)
        {
            Handler += callback;
        }
    }
}
