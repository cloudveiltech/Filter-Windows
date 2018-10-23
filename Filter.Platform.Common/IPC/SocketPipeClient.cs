// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using Citadel.IPC.Messages;
using Filter.Platform.Common.Util;

namespace Filter.Platform.Common.IPC
{
    public class SocketPipeClient : IPipeClient
    {
        private IPathProvider paths;
        private NLog.Logger logger;

        public SocketPipeClient()
        {
            paths = PlatformTypes.New<IPathProvider>();
            logger = LoggerUtil.GetAppWideLogger();
        }

        public bool AutoReconnect { get; set; }
        private int reconnectTries = 0;

        public event ClientConnectionHandler Connected;
        public event ClientConnectionHandler Disconnected;
        public event ServerMessageHandler ServerMessage;
        public event PipeExceptionHandler Error;

        Socket clientSocket;
        ClientRepresentation client;

        public void PushMessage(BaseMessage msg)
        {
            logger.Info($"PushMessage({msg.GetType().Name})");

            IFormatter formatter = new BinaryFormatter();

            using (MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, msg);

                byte[] arr = stream.ToArray();

                byte[] message = SocketPipeHelper.BuildMessage(msg is ClientToClientMessage ? MessageType.BroadcastMessage : MessageType.Message, arr);
                client.SendBytes(message);
            }
        }

        internal void OnDisconnected(ClientRepresentation client)
        {
            Disconnected?.Invoke();

            if(AutoReconnect && reconnectTries < 10)
            {
                Thread.Sleep(5);
                reconnectTries++;
                Start();
            }
        }

        internal void ProcessMessageBytes(byte[] buffer)
        {
            logger.Info("Message bytes received.");

            IFormatter formatter = new BinaryFormatter();

            MessageType type = SocketPipeHelper.GetMessageType(buffer);
            if (type == MessageType.Message || type == MessageType.BroadcastMessage)
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(buffer, 8, buffer.Length - 8);
                    ms.Position = 0;

                    object o = formatter.Deserialize(ms);

                    if (o is BaseMessage)
                    {
                        ProcessMessage(o as BaseMessage);
                    }
                }
            }
            else if(type == MessageType.ConnectionAccepted)
            {
                Connected?.Invoke();
            }
        }

        public void Start()
        {
            clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            int ipcPort = readPort();

            try
            {
                clientSocket.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), ipcPort));

                client = new ClientRepresentation(this);
                client.ClientSocket = clientSocket;
                client.MessageReceived += ProcessMessageBytes;

                client.AwaitBytes();
            }
            catch(Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
            }

            
        }

        internal void ProcessMessage(BaseMessage msg)
        {
            this.ServerMessage?.Invoke(msg);
        }

        public void Stop()
        {
            client.ClientSocket.Close();
        }

        public void WaitForConnection()
        {

        }

        private int readPort()
        {
            string path = Path.Combine(paths.ApplicationDataFolder, ".ipc-port");

            try
            {
                string text = File.ReadAllText(path);

                int port;
                if(int.TryParse(text, out port))
                {
                    return port;
                }
            }
            catch
            {
            }

            // Invalid parsing, return default port.
            return 14302;
        }
    }
}
