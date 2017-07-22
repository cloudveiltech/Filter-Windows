/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Te.Citadel.Util;
using System.IO.Pipes;
using Newtonsoft.Json;
using System.IO;
using System.Security.Principal;
using System.Security.AccessControl;
using NamedPipeWrapper;

namespace Te.Citadel.IPC
{
    /// <summary>
    /// Delegate used to handle the show window command event.
    /// </summary>
    public delegate void IPCShowWindowCommandMsgHandler();

    internal class IPCServer : IDisposable
    {

        public event IPCShowWindowCommandMsgHandler ShowWindowCommandRecieved;

        private NamedPipeWrapper.NamedPipeServer<IPCMessage> m_server;

        private Logger m_logger;

        private object m_lock = new object();

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="">
        /// Will throw if pipe already exists.
        /// </exception>
        public IPCServer(string channel)
        {
            m_logger = LoggerUtil.GetAppWideLogger();

            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);

            var users = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);

            PipeSecurity sec = null;

            using(var npc = new NamedPipeServerStream(Path.GetRandomFileName(), PipeDirection.InOut, -1, PipeTransmissionMode.Message, PipeOptions.Asynchronous, 65536, 65536))
            {
                sec = npc.GetAccessControl();
            }
               
            sec.PurgeAccessRules(everyone);
            sec.PurgeAuditRules(everyone);
            sec.PurgeAccessRules(users);
            sec.PurgeAuditRules(users);
            //sec.SetAccessRule(new PipeAccessRule(everyone, PipeAccessRights.ReadWrite, AccessControlType.Allow));
            sec.SetAccessRule(new PipeAccessRule(everyone, PipeAccessRights.FullControl, AccessControlType.Allow));
            sec.SetAccessRule(new PipeAccessRule(users, PipeAccessRights.FullControl, AccessControlType.Allow));

            m_server = new NamedPipeServer<IPCMessage>(channel, sec);
            m_server.ClientConnected += OnClientConnected;
            m_server.ClientMessage += OnClientMessage;
            m_server.Start();
        }

        private void OnClientMessage(NamedPipeConnection<IPCMessage, IPCMessage> connection, IPCMessage message)
        {
            m_logger.Info("Client command.");

            switch(message.Cmd)
            {
                case IPCCommand.ShowWindow:
                {
                    m_logger.Info("Client show window command.");
                    ShowWindowCommandRecieved?.Invoke();
                }
                break;

                default:
                {
                    m_logger.Info("Unknown client command.");
                }
                break;
            }
        }

        private void OnClientConnected(NamedPipeConnection<IPCMessage, IPCMessage> connection)
        {
            ShowWindowCommandRecieved?.Invoke();

            m_logger.Info("Named pipe client connected to channel.");
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if(!disposedValue)
            {
                if(disposing)
                {
                    if(m_server != null)
                    {
                        try
                        {
                            m_server.Stop();
                        }
                        catch { }

                        m_server = null;
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~IPCServer() {
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
        #endregion
    }
}
