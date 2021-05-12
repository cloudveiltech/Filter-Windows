using CloudVeil.IPC.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CloudVeil.IPC
{
    public delegate bool OnReplyHandler(ReplyHandlerClass h, IpcMessage message);
    public delegate bool OnReplyHandler<T>(ReplyHandlerClass<T> h, IpcMessage<T> message);

    /// <summary>
    /// This keeps track of a callback for a reply to a certain IPC message.
    /// Allows us to use a fluent API for request-response on the IPC, which will enable more closely coupled code and make cause and effect clearer.
    /// </summary>
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

    
    public class ReplyHandlerClass
    {
        public ReplyHandlerClass(IpcCommunicator comm)
        {
            Communicator = comm;
            lifetime = Stopwatch.StartNew();
        }

        private Stopwatch lifetime = null;

        public IpcCommunicator Communicator { get; set; }

        public event OnReplyHandler Handler;

        public event Action<BaseMessage> BaseHandler;

        public bool ExpectsReply { get; private set; } = false;

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
                if (BaseHandler != null)
                {
                    BaseHandler.Invoke(msg);
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Use this function to register a reply callback.
        /// </summary>
        /// <param name="callback"></param>
        public virtual void OnReply(OnReplyHandler callback)
        {
            Handler += callback;
            ExpectsReply = true;
        }

        public virtual void OnBaseReply(Action<BaseMessage> callback)
        {
            BaseHandler += callback;
            ExpectsReply = true;
        }

        public bool DisposeIfDiscarded()
        {
            if (!ExpectsReply && lifetime.ElapsedMilliseconds > 2000)
            {
                lifetime.Stop();
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
