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
using System.Threading;
using Citadel.IPC.Messages;
using Filter.Platform.Common.IPC;

namespace Filter.Platform.Mac
{
    public class MacPipeClient : IPipeClient, IDisposable
    {
        public MacPipeClient(string channel)
        {
            this.channel = channel;
        }

        private IntPtr handle;
        private IntPtr thread;

        private string channel;
        private bool isConnected = false;
        private EventWaitHandle connectionWaitHandle = null;

        public bool AutoReconnect { get; set; }

        public event ClientConnectionHandler Connected;
        public event ClientConnectionHandler Disconnected;
        public event ServerMessageHandler ServerMessage;
        public event PipeExceptionHandler Error;

        private void onConnected()
        {
            Volatile.Write(ref isConnected, true);
            if(connectionWaitHandle != null)
            {
                connectionWaitHandle.Set();
            }

            Connected?.Invoke();
        }

        private void onDisconnected()
        {
            // This doesn't get called due to not being able to detect whether the other mach port is open.
            isConnected = false;
            Disconnected?.Invoke();
        }

        private void onIncomingMessage(IntPtr arr, int length)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                byte[] buf = new byte[length];

                Marshal.Copy(arr, buf, 0, length);
                stream.Write(buf, 0, length);
                stream.Position = 0;

                IFormatter formatter = new BinaryFormatter();
                BaseMessage msg = formatter.Deserialize(stream) as BaseMessage;

                ServerMessage?.Invoke(msg);
            }
        }

        public void PushMessage(BaseMessage msg)
        {
            IFormatter formatter = new BinaryFormatter();

            using (MemoryStream stream = new MemoryStream())
            {
                formatter.Serialize(stream, msg);

                byte[] arr = stream.ToArray();

                bool isBroadcast = msg is ClientToClientMessage;

                NativeIPCClientImpl.Send(handle, arr, arr.Length, isBroadcast);
            }
        }

        public void Start()
        {
            handle = NativeIPCClientImpl.CreateIPCClient(onIncomingMessage, onConnected, onDisconnected);
            thread = NativeIPCClientImpl.StartLoop(handle);

            NativeIPCClientImpl.Connect(handle, "org.cloudveil.filterserviceprovider");
        }

        public void Stop()
        {
            NativeIPCClientImpl.StopLoop(thread);

            NativeIPCClientImpl.Release(handle);
            handle = IntPtr.Zero;
        }

        public void WaitForConnection()
        {
            bool connected = Volatile.Read(ref isConnected);
            if (!connected)
            {
                connectionWaitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
                connectionWaitHandle.WaitOne();
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                if (handle != IntPtr.Zero)
                {
                    NativeIPCClientImpl.Release(handle);
                    handle = IntPtr.Zero;
                }

                disposedValue = true;
            }
        }

        ~MacPipeClient()
        {
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
