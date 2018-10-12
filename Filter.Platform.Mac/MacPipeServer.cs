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
    internal delegate void ConnectionDelegate();
    internal delegate void IncomingMessageDelegate(IntPtr message, int length);

    internal static class PipeServerInterop
    {
        internal const string PlatformLib = "Filter.Platform.Mac.Native";

        [DllImport(PlatformLib)]
        internal static extern IntPtr GetGlobalIPCServer();

        [DllImport(PlatformLib)]
        internal static extern void StopIPCServer(IntPtr handle);

        [DllImport(PlatformLib, EntryPoint = "IPCServer_SetOnClientConnected")]
        internal static extern void SetOnClientConnected(IntPtr handle, ConnectionDelegate onClientConnected);

        [DllImport(PlatformLib, EntryPoint = "IPCServer_SetOnClientDisconnected")]
        internal static extern void SetOnClientDisconnected(IntPtr handle, ConnectionDelegate onClientDisconnected);

        [DllImport(PlatformLib, EntryPoint = "IPCServer_SetOnIncomingMessage")]
        internal static extern void SetOnIncomingMessage(IntPtr handle, IncomingMessageDelegate onIncomingMessage);

        [DllImport(PlatformLib, EntryPoint = "IPCServer_PushMessage")]
        internal static extern bool PushMessage(IntPtr handle, byte[] message, int length);
    }

    public class MacPipeServer : IPipeServer
    {
        public MacPipeServer()
        {

        }

        private IntPtr serverHandle;

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

                PipeServerInterop.PushMessage(serverHandle, arr, arr.Length);
            }
        }

        public void Start()
        {
            serverHandle = PipeServerInterop.GetGlobalIPCServer();

            if (serverHandle == IntPtr.Zero)
            {
                throw new Exception("Failed to initialize global IPC Server");
            }

            PipeServerInterop.SetOnClientConnected(serverHandle, this.onClientConnected);
            PipeServerInterop.SetOnClientDisconnected(serverHandle, this.onClientDisconnected);
            PipeServerInterop.SetOnIncomingMessage(serverHandle, this.onIncomingMessage);
        }

        public void Stop()
        {
            PipeServerInterop.StopIPCServer(serverHandle);
            serverHandle = IntPtr.Zero;
        }
    }
}
