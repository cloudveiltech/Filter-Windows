using Citadel.IPC.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common.IPC
{
    public delegate void ClientConnectionHandler();
    public delegate void ServerMessageHandler(BaseMessage msg);

    public interface IPipeClient
    {
        event ClientConnectionHandler Connected;
        event ClientConnectionHandler Disconnected;

        event ServerMessageHandler ServerMessage;
        event PipeExceptionHandler Error;

        bool AutoReconnect { get; set; }

        void Start();
        void Stop();

        void WaitForConnection();

        void PushMessage(BaseMessage msg);
    }
}
