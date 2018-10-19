// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Citadel.IPC.Messages;
using Filter.Platform.Common.IPC;
using Foundation;

namespace Filter.Platform.Mac
{
    public class MacPipeServer : IPipeServer
    {
        public MacPipeServer()
        {

        }

        private IntPtr serverHandle;
        private IntPtr serverThread;

        public event ConnectionHandler ClientConnected;
        public event ConnectionHandler ClientDisconnected;
        public event MessageHandler ClientMessage;
        public event PipeExceptionHandler Error;

        private void onClientConnected()
        {
            ClientConnected?.Invoke(this);
        }

        private void onClientDisconnected()
        {
            ClientDisconnected?.Invoke(this);
        }

        private void onIncomingMessage(IntPtr arr, int length)
        {
            using(MemoryStream stream = new MemoryStream())
            {
                byte[] buf = new byte[length];

                Marshal.Copy(arr, buf, 0, length);
                stream.Write(buf, 0, length);
                stream.Position = 0;

                IFormatter formatter = new BinaryFormatter();
                BaseMessage msg = formatter.Deserialize(stream) as BaseMessage;

                ClientMessage?.Invoke(this, msg);
            }
        }

        public void PushMessage(BaseMessage msg)
        {
            IFormatter formatter = new BinaryFormatter();

            using(MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, msg);

                byte[] arr = stream.ToArray();

                NativeIPCServerImpl.SendToAll(serverHandle, arr, arr.Length);
            }
        }

        public void Start()
        {
            serverHandle = NativeIPCServerImpl.CreateIPCServer("org.cloudveil.filterserviceprovider", onIncomingMessage, onClientConnected, onClientDisconnected);

            if (serverHandle == IntPtr.Zero)
            {
                throw new Exception("Failed to initialize global IPC Server");
            }

            serverThread = NativeIPCServerImpl.StartLoop(serverHandle);
        }

        public void Stop()
        {
            NativeIPCServerImpl.StopLoop(serverThread);

            NativeIPCServerImpl.Release(serverHandle);
            serverHandle = IntPtr.Zero;
        }
    }
}
