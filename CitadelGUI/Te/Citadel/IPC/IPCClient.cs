/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Te.Citadel.Util;
using System.ComponentModel;
using Newtonsoft.Json;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Threading;
using NamedPipeWrapper;

namespace Te.Citadel.IPC
{
    internal class IPCClient : IDisposable
    {
        private NamedPipeClient<IPCMessage> m_client;

        private Logger m_logger;

        public IPCClient(string channel)
        {   
            m_logger = LoggerUtil.GetAppWideLogger();

            m_client = new NamedPipeClient<IPCMessage>(channel);
            m_client.Start();

            m_client.ServerMessage += OnMessageReceived;
        }

        private void OnMessageReceived(NamedPipeConnection<IPCMessage, IPCMessage> connection, IPCMessage message)
        {   
            m_logger.Info("Client received message: {0}", message.Cmd);
        }

        public void SendMessage(IPCMessage msg)
        {
            m_client.PushMessage(msg);
        }

        public bool UseCompression
        {
            get;
            set;
        }

        public Action<object> Connected
        {
            get;
            set;
        }

        public Action<object, Exception> Disconnected
        {
            get;
            set;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if(!disposedValue)
            {
                if(disposing)
                {
                    if(m_client != null)
                    {
                        try
                        {
                            m_client.Stop();
                            m_client = null;
                        }
                        catch { }
                        m_client = null;
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~IPCClient() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        public void Initialize(PipeStream stream, bool isServerSide)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
