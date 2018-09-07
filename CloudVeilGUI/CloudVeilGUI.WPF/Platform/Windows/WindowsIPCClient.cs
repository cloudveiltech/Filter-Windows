using Citadel.IPC;
using System;
using NamedPipeWrapper;
using Citadel.IPC.Messages;
using Citadel.Core.Windows.Util;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace CloudVeilGUI.Platform.Windows
{
    public class WindowsIPCClient : IPCClient
    {
        private NamedPipeClient<BaseMessage> client;

        public WindowsIPCClient(bool autoReconnect = false) : base(autoReconnect)
        {
            var channel = string.Format("{0}.{1}", nameof(Citadel.IPC), FingerPrint.Value).ToLower();

            client = new NamedPipeClient<BaseMessage>(channel);

            logger.Info("Creating client");

            client.Connected += OnClientConnected;
            client.Disconnected += OnClientDisconnected;
            client.ServerMessage += OnClientReceivedServerMessage;
            client.AutoReconnect = autoReconnect;

            client.Error += clientError;

            client.Start();
        }

        private void clientError(Exception ex)
        {
            LoggerUtil.RecursivelyLogException(logger, ex);
        }

        public override void WaitForConnection()
        {
            client.WaitForConnection();
        }

        private void OnClientConnected(NamedPipeConnection<BaseMessage, BaseMessage> connection)
        {
            base.OnConnected();
        }

        private void OnClientDisconnected(NamedPipeConnection<BaseMessage, BaseMessage> connection)
        {
            base.OnDisconnected();
        }

        private void OnClientReceivedServerMessage(NamedPipeConnection<BaseMessage, BaseMessage> connection, BaseMessage message)
        {
            base.OnServerMessage(message);
        }

        protected override void PushMessage(BaseMessage msg, GenericReplyHandler replyHandler = null)
        {
            var bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, msg);
            }

            client.PushMessage(msg);

            if (replyHandler != null)
            {
                ipcQueue.AddMessage(msg, replyHandler);
            }
        }

        private bool disposedValue = false; // To detect redundant calls
        protected override void Dispose(bool disposing)
        {
            if(!disposedValue)
            {
                base.Dispose(disposing);

                if(disposing)
                {
                    if(client != null)
                    {
                        client.AutoReconnect = false;
                        client.Stop();
                        client = null;
                    }
                }

                disposedValue = true;
            }
        }
    }
}
