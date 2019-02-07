using Citadel.IPC.Messages;
using Filter.Platform.Common.Util;
using System;
using System.Collections.Generic;
using System.Text;

namespace Citadel.IPC
{
    public delegate bool IpcMessageHandler(IpcMessage message);
    public delegate bool IpcMessageHandler<T>(IpcMessage<T> message);

    /// <summary>
    /// The goal of class was to reduce the amount of different messages and to allow us to pass primitives back and forth while using a fluent API for responses to requests and sends.
    /// </summary>
    public abstract class IpcCommunicator
    {
        private NLog.Logger m_logger;

        public IpcCommunicator()
        {
            m_logger = LoggerUtil.GetAppWideLogger();
        }

        protected Dictionary<IpcCall, IpcMessageHandler> sendHandlers = new Dictionary<IpcCall, IpcMessageHandler>();
        protected Dictionary<IpcCall, IpcMessageHandler> requestHandlers = new Dictionary<IpcCall, IpcMessageHandler>();

        protected abstract ReplyHandlerClass RequestInternal(IpcCall call, object data, BaseMessage replyTo);
        protected abstract ReplyHandlerClass SendInternal(IpcCall call, object data, BaseMessage replyTo);

        /// <summary>
        /// Use this to send a strongly typed request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="call"></param>
        /// <param name="data"></param>
        /// <param name="replyTo"></param>
        /// <returns></returns>
        public ReplyHandlerClass<TResponse> Request<T, TResponse>(IpcCall call, T data, BaseMessage replyTo = null)
            => new ReplyHandlerClass<TResponse>(RequestInternal(call, data, replyTo));


        public ReplyHandlerClass<TResponse> Send<T, TResponse>(IpcCall call, T data, BaseMessage replyTo = null)
            => new ReplyHandlerClass<TResponse>(SendInternal(call, data, replyTo));

        public ReplyHandlerClass Request<T>(IpcCall call, T data, BaseMessage replyTo = null)
        {
            return RequestInternal(call, data, replyTo);
        }

        /// <summary>
        /// Use this to send a strongly typed notification. Please use this rather than the weakly typed Send() function so as to reduce mismatched type errors.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="call"></param>
        /// <param name="data"></param>
        /// <param name="replyTo"></param>
        /// <returns></returns>
        public ReplyHandlerClass Send<T>(IpcCall call, T data, BaseMessage replyTo = null)
        {
            return SendInternal(call, data, replyTo);
        }

        // TODO: Figure out how to handle generics here.

        public void RegisterRequestHandler(IpcCall call, IpcMessageHandler handler)
        {
            requestHandlers[call] = handler;
        }

        public void RegisterSendHandler(IpcCall call, IpcMessageHandler handler)
        {
            sendHandlers[call] = handler;
        }

        public void RegisterRequestHandler<T>(IpcCall call, IpcMessageHandler<T> handler)
        {
            requestHandlers[call] = (msg) =>
            {
                return handler?.Invoke(msg.As<T>()) ?? false;
            };
        }

        public void RegisterSendHandler<T>(IpcCall call, IpcMessageHandler<T> handler)
        {
            sendHandlers[call] = (msg) =>
            {
                return handler?.Invoke(msg.As<T>()) ?? false;
            };
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
