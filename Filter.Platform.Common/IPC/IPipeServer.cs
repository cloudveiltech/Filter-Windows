using Citadel.IPC.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common.IPC
{
    public delegate void ConnectionHandler(IPipeServer server);
    public delegate void MessageHandler(IPipeServer server, BaseMessage message);
    public delegate void PipeExceptionHandler(Exception exception);

    public interface IPipeServer
    {
        event ConnectionHandler ClientConnected;
        event ConnectionHandler ClientDisconnected;
        event MessageHandler ClientMessage;
        event PipeExceptionHandler Error;

        void Start();
        void Stop();

        void PushMessage(BaseMessage msg);
    }
}
