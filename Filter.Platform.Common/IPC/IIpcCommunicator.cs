using Citadel.IPC.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Citadel.IPC
{
    public delegate bool IpcMessageHandler(IpcMessage message);

    public interface IIpcCommunicator
    {
        ReplyHandlerClass Request(IpcCall call, object data, BaseMessage replyTo);
        ReplyHandlerClass Send(IpcCall call, object data, BaseMessage replyTo);

        void RegisterRequestHandler(IpcCall call, IpcMessageHandler handler);
        void RegisterSendHandler(IpcCall call, IpcMessageHandler handler);

        bool HandleIpcMessage(BaseMessage ipcMessage);
    }

    public abstract class IpcCommunicator : IIpcCommunicator
    {
        protected Dictionary<IpcCall, IpcMessageHandler> sendHandlers = new Dictionary<IpcCall, IpcMessageHandler>();
        protected Dictionary<IpcCall, IpcMessageHandler> requestHandlers = new Dictionary<IpcCall, IpcMessageHandler>();

        public abstract ReplyHandlerClass Request(IpcCall call, object data, BaseMessage replyTo);
        public abstract ReplyHandlerClass Send(IpcCall call, object data, BaseMessage replyTo);

        public void RegisterRequestHandler(IpcCall call, IpcMessageHandler handler)
        {
            sendHandlers[call] = handler;
        }

        public void RegisterSendHandler(IpcCall call, IpcMessageHandler handler)
        {
            requestHandlers[call] = handler;
        }

        public bool HandleIpcMessage(BaseMessage baseMessage)
        {
            IpcMessage message = baseMessage as IpcMessage;
            if(message == null)
            {
                return false;
            }

            IpcMessageHandler handler = null;

            if(message.Method == IpcMessageMethod.Send)
            {
                sendHandlers.TryGetValue(message.Call, out handler);
            }
            else if(message.Method == IpcMessageMethod.Request)
            {
                requestHandlers.TryGetValue(message.Call, out handler);
            }

            return handler?.Invoke(message) ?? false;
        }
    }
}
