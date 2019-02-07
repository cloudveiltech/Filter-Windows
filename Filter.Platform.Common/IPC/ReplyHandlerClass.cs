using Citadel.IPC.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Citadel.IPC
{
    public delegate bool OnReplyHandler(ReplyHandlerClass h, IpcMessage message);
    public delegate bool OnReplyHandler<T>(ReplyHandlerClass<T> h, IpcMessage<T> message);

    public class ReplyHandlerClass<T> : ReplyHandlerClass
    {
        /// <summary>
        /// The un-typed inner class.
        /// </summary>

        public ReplyHandlerClass(ReplyHandlerClass inner) : base(inner.Communicator)
        {
            
        }

        /// <summary>
        /// Use this to register a reply callback.
        /// </summary>
        /// <param name="callback"></param>
        public void OnReply(OnReplyHandler<T> callback)
        {
            base.OnReply((h, msg) =>
            {
                return callback?.Invoke(this, msg.As<T>()) ?? false;
            });
        }
    }

    /// <summary>
    /// This keeps track of a callback for a reply to a certain IPC message.
    /// Allows us to use a fluent API for request-response on the IPC, which will enable more closely coupled code and make cause and effect clearer.
    /// </summary>
    public class ReplyHandlerClass
    {
        public ReplyHandlerClass(IpcCommunicator comm)
        {
            Communicator = comm;
        }

        public IpcCommunicator Communicator { get; set; }

        public event OnReplyHandler Handler;

        /// <summary>
        /// This is used by the IPC client and server as a callback when a reply is received to a specific message.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public bool TriggerHandler(BaseMessage msg)
        {
            if(msg is IpcMessage)
            {
                return Handler?.Invoke(this, msg as IpcMessage) ?? false;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Use this function to register a reply callback.
        /// </summary>
        /// <param name="callback"></param>
        public virtual void OnReply(OnReplyHandler callback)
        {
            Handler += callback;
        }
    }
}
