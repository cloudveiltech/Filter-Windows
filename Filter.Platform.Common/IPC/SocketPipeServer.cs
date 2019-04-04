// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

using Citadel.IPC.Messages;
using Filter.Platform.Common.Util;

namespace Filter.Platform.Common.IPC
{
    public delegate void MessageBytesHandler(byte[] bytes);

    class ClientRepresentation
    {
        public ClientRepresentation(SocketPipeServer server) : this()
        {
            this.server = server;
        }

        public ClientRepresentation(SocketPipeClient client) : this()
        {
            this.client = client;
        }

        public ClientRepresentation()
        {
            BytesReceived = 0;
            IsReceivingMessage = false;
            CompletedBufferLength = 0;
            MyBuffer = null;
            receiveResult = null;
        }

        private SocketPipeServer server;
        private SocketPipeClient client;

        public Socket ClientSocket { get; set; }
        public byte[] MyBuffer { get; set; }
        public int BytesReceived { get; set; }

        public bool IsReceivingMessage { get; set; }
        public int CompletedBufferLength { get; set; }

        private bool isInvalidSocket = false;

        private IAsyncResult receiveResult;
        private byte[] receiveBuffer;

        public event MessageBytesHandler MessageReceived;

        public void SendBytes(byte[] msg)
        {
            try
            {
                ClientSocket.Send(msg);
            }
            catch (SocketException)
            {
                isInvalidSocket = true;
            }
        }

        public void AwaitBytes()
        {
            if(server != null && server.isStopped)
            {
                return;
            }

            if (receiveResult == null)
            {
                receiveBuffer = receiveBuffer ?? new byte[4096];
                SocketError error;

                receiveResult = ClientSocket.BeginReceive(receiveBuffer, 0, 4096, SocketFlags.None, out error, HandleAsyncCallback, null);

                if(error != SocketError.Success && error != SocketError.IOPending)
                {
                    if (server != null)
                    {
                        server.RemoveClient(this);
                    }

                    if(client != null)
                    {
                        client.OnDisconnected(this);
                    }
                }
            }
            else
            {

            }
        }

        private void HandleAsyncCallback(IAsyncResult ar)
        {
            SocketError error;
            bool continueAwaitingBytes = true;

            int receiveRet = 0, receiveBufferIdx = 0;

            try
            {
                receiveRet = ClientSocket.EndReceive(ar, out error);
            }
            catch(ObjectDisposedException)
            {
                if(client != null)
                {
                    client.OnDisconnected(this);
                }

                return; // No need for AwaitBytes() on this socket as it is now closed.
            }

            receiveResult = null;

            // There might be the end of one message and the start of another in the receive buffer.


            if (error != SocketError.Success)
            {
                server.RemoveClient(this);
                continueAwaitingBytes = false;
            }
            else if (receiveRet != 0)
            {
                int loops = 0;
                // Process messages.
                while(true)
                {
                    loops++;
                    if((loops % 10) == 0)
                    {
                        Console.WriteLine("Number of while(true) loops = {0}", loops);
                    }

                    // If buffer is null, see if this is the beginning of a message.
                    if (MyBuffer == null)
                    {
                        if (receiveBuffer[receiveBufferIdx] == SocketPipeHelper.MagicByte)
                        {
                            int length = 0;
                            length = BitConverter.ToInt32(receiveBuffer, receiveBufferIdx + 4);

                            MyBuffer = new byte[length + 8];
                            CompletedBufferLength = length + 8;
                        }
                    }

                    if (MyBuffer != null)
                    {
                        try
                        {
                            // Make sure that length left doesn't overflow MyBuffer.Length
                            int lengthLeft = BytesReceived + receiveRet > MyBuffer.Length ? MyBuffer.Length - BytesReceived : receiveRet - BytesReceived;

                            Buffer.BlockCopy(receiveBuffer, receiveBufferIdx, MyBuffer, BytesReceived, lengthLeft);
                            BytesReceived += lengthLeft;

                            if (BytesReceived >= CompletedBufferLength)
                            {
                                handleBuffer(MyBuffer);
                                MyBuffer = null;
                                BytesReceived = 0;
                            }

                            receiveBufferIdx += lengthLeft;

                            // Check to see if we've processed all of the incoming messages in this buffer.
                            if(receiveBufferIdx >= receiveRet)
                            {
                                // If we have, break the while loop and await.
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), ex);
                        }
                    }
                }

            }

            if(continueAwaitingBytes)
            {
                AwaitBytes();
            }
        }

        void handleBuffer(byte[] buffer)
        {
            try
            {
                MessageReceived?.Invoke(buffer);
            }
            catch(Exception ex)
            {
                LoggerUtil.RecursivelyLogException(LoggerUtil.GetAppWideLogger(), ex);
                // Swallow invalid messages.
            }
        }
    }

    public class SocketPipeServer : IPipeServer
    {
        public event ConnectionHandler ClientConnected;
        public event ConnectionHandler ClientDisconnected;
        public event MessageHandler ClientMessage;
        public event PipeExceptionHandler Error;

        private Socket serverSocket;
        private List<ClientRepresentation> connectedClients;

        private IPathProvider paths;
        private NLog.Logger logger;

        internal bool isStopped;

        public SocketPipeServer()
        {
            paths = PlatformTypes.New<IPathProvider>();
            logger = LoggerUtil.GetAppWideLogger();
            isStopped = false;

            connectedClients = new List<ClientRepresentation>();
        }

        public void PushMessage(BaseMessage msg)
        {
            try
            {
                logger.Info($"PushMessage({msg.GetType().Name}) {connectedClients.Count}");

                IFormatter formatter = new BinaryFormatter();

                using (MemoryStream stream = new MemoryStream())
                {
                    formatter.Serialize(stream, msg);

                    byte[] arr = stream.ToArray();

                    byte[] message = SocketPipeHelper.BuildMessage(MessageType.Message, arr);
                    foreach (var client in connectedClients)
                    {
                        client.SendBytes(message);
                    }
                }
            }
            catch(Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
            }
        }

        public void Start()
        {
            isStopped = false;

            try
            {
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                serverSocket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0));

                publishSocketPort();

                serverSocket.Listen(64);

                Task.Run(() => { serverAccept(); });
            }
            catch(Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
            }
        }

        public void Stop()
        {
            isStopped = true;

            serverSocket.Close();
        }

        internal void RemoveClient(ClientRepresentation client)
        {
            Console.WriteLine("Removing a client --------");
            this.connectedClients.Remove(client);
            this.ClientDisconnected?.Invoke(this);
        }

        internal void ProcessMessageBytes(byte[] buffer)
        {
            logger.Info("ProcessMessageBytes()");

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
        }

        internal void ProcessMessage(BaseMessage msg)
        {
            this.ClientMessage?.Invoke(this, msg);
        }

        private void publishSocketPort()
        {
            int myPort = ((IPEndPoint)serverSocket.LocalEndPoint).Port;

            try
            {
                string publishPath = Path.Combine(paths.ApplicationDataFolder, ".ipc-port");

                File.WriteAllText(publishPath, myPort.ToString());
            }
            catch (Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
            }
        }

        private void serverAccept()
        {
            try
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                args.Completed += acceptCompletedHandler;

                bool isAsync = false;

                while (!(isAsync = serverSocket.AcceptAsync(args)))
                {
                    // Accept completed synchronously.
                    if (args.SocketError == SocketError.Success)
                    {
                        acceptCompleted(args.AcceptSocket);
                        args.AcceptSocket = null;
                    }
                }
            }
            catch(Exception ex)
            {
                LoggerUtil.RecursivelyLogException(logger, ex);
            }
            // serverAccept is called by acceptCompleted
        }

        void acceptCompleted(Socket accepted)
        {
            // Send a ConnectionAccepted.

            try
            {
                ClientRepresentation client = new ClientRepresentation(this)
                {
                    ClientSocket = accepted
                };

                connectedClients.Add(client);

                client.MessageReceived += ProcessMessageBytes;
                client.SendBytes(SocketPipeHelper.BuildMessage(MessageType.ConnectionAccepted, null));

                ClientConnected?.Invoke(this);

                client.AwaitBytes();

            }
            finally
            {
                if (!isStopped)
                {
                    serverAccept();
                }
            }
        }

        void acceptCompletedHandler(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                if (e.SocketError == SocketError.Success)
                {
                    acceptCompleted(e.AcceptSocket);
                }
            }
            finally
            {
                if (!isStopped)
                {
                    serverAccept();
                }
            }
        }

    }
}
 