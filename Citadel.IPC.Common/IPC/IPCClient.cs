/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Util;
using Citadel.IPC.Messages;
using NamedPipeWrapper;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Text;
using System.Threading.Tasks;


namespace Citadel.IPC
{   
    public class StateChangeEventArgs : EventArgs
    {
        public FilterStatus State
        {
            get;
            private set;
        }

        public TimeSpan CooldownPeriod
        {
            get;
            private set;
        } = TimeSpan.Zero;

        public StateChangeEventArgs(FilterStatusMessage msg)
        {
            State = msg.Status;
            CooldownPeriod = msg.CooldownDuration;
        }
    }

    public delegate void StateChangeHandler(StateChangeEventArgs args);

    public delegate void AuthenticationResultHandler(AuthenticationAction result);

    public delegate void DeactivationRequestResultHandler(bool granted);

    public delegate void BlockActionReportHandler(NotifyBlockActionMessage msg);

    public delegate void RelaxedPolicyInfoReceivedHandler(RelaxedPolicyMessage msg);

    public delegate void ClientGenericParameterlessHandler();

    public delegate void ServerUpdateRequestHandler(ServerUpdateQueryMessage msg);

    public class IPCClient : IDisposable
    {

        private NamedPipeClient<BaseMessage> m_client;

        public ClientGenericParameterlessHandler ConnectedToServer;

        public ClientGenericParameterlessHandler DisconnectedFromServer;

        public StateChangeHandler StateChanged;

        public AuthenticationResultHandler AuthenticationResultReceived;

        public DeactivationRequestResultHandler DeactivationResultReceived;

        public BlockActionReportHandler BlockActionReceived;

        public ClientGenericParameterlessHandler RelaxedPolicyExpired;

        public RelaxedPolicyInfoReceivedHandler RelaxedPolicyInfoReceived;

        public ClientToClientMessageHandler ClientToClientCommandReceived;

        public ServerUpdateRequestHandler ServerAppUpdateRequestReceived;

        public ClientGenericParameterlessHandler ServerUpdateStarting;

        /// <summary>
        /// Our logger.
        /// </summary>
        private readonly Logger m_logger;

        public IPCClient(bool autoReconnect = false)
        {
            m_logger = LoggerUtil.GetAppWideLogger();

            var channel = string.Format("{0}.{1}", nameof(Citadel.IPC), FingerPrint.Value).ToLower();

            m_logger.Info("Creating client.");

            m_client = new NamedPipeClient<BaseMessage>(channel);
            
            m_client.Connected += OnConnected;
            m_client.Disconnected += OnDisconnected;
            m_client.ServerMessage += OnServerMessage;
            m_client.AutoReconnect = autoReconnect;

            m_client.Error += M_client_Error;

            m_client.Start();
        }

        public void WaitForConnection()
        {
            m_client.WaitForConnection();
        }

        private void M_client_Error(Exception exception)
        {   
            LoggerUtil.RecursivelyLogException(m_logger, exception);
        }

        private void OnConnected(NamedPipeConnection<BaseMessage, BaseMessage> connection)
        {
            ConnectedToServer?.Invoke();
        }

        private void OnDisconnected(NamedPipeConnection<BaseMessage, BaseMessage> connection)
        {
            DisconnectedFromServer?.Invoke();
        }

        private void OnServerMessage(NamedPipeConnection<BaseMessage, BaseMessage> connection, BaseMessage message)
        {
            // This is so gross, but unfortuantely we can't just switch on a type.
            // We can come up with a nice mapping system so we can do a switch,
            // but this can wait.

            m_logger.Debug("Got IPC message from server.");

            var msgRealType = message.GetType();

            if(msgRealType == typeof(Messages.AuthenticationMessage))
            {
                m_logger.Debug("Server message is {0}", nameof(Messages.AuthenticationMessage));
                var cast = (Messages.AuthenticationMessage)message;
                if(cast != null)
                {   
                    AuthenticationResultReceived?.Invoke(cast.Action);
                }
            }
            else if(msgRealType == typeof(Messages.DeactivationMessage))
            {
                m_logger.Debug("Server message is {0}", nameof(Messages.DeactivationMessage));
                var cast = (Messages.DeactivationMessage)message;
                if(cast != null)
                {   
                    DeactivationResultReceived?.Invoke(cast.Command == DeactivationCommand.Granted);
                }
            }
            else if(msgRealType == typeof(Messages.FilterStatusMessage))
            {
                m_logger.Debug("Server message is {0}", nameof(Messages.FilterStatusMessage));
                var cast = (Messages.FilterStatusMessage)message;
                if(cast != null)
                {   
                    StateChanged?.Invoke(new StateChangeEventArgs(cast));
                }
            }
            else if(msgRealType == typeof(Messages.NotifyBlockActionMessage))
            {
                m_logger.Debug("Server message is {0}", nameof(Messages.NotifyBlockActionMessage));
                var cast = (Messages.NotifyBlockActionMessage)message;
                if(cast != null)
                {   
                    BlockActionReceived?.Invoke(cast);
                }
            }
            else if(msgRealType == typeof(Messages.RelaxedPolicyMessage))
            {
                m_logger.Debug("Server message is {0}", nameof(Messages.RelaxedPolicyMessage));
                var cast = (Messages.RelaxedPolicyMessage)message;
                if(cast != null)
                {   
                    switch(cast.Command)
                    {
                        case RelaxedPolicyCommand.Info:
                        {
                            RelaxedPolicyInfoReceived?.Invoke(cast);
                        }
                        break;

                        case RelaxedPolicyCommand.Expired:
                        {
                            RelaxedPolicyExpired?.Invoke();
                        }
                        break;
                    }
                }
            }
            else if(msgRealType == typeof(Messages.ClientToClientMessage))
            {
                m_logger.Debug("Server message is {0}", nameof(Messages.ClientToClientMessage));
                var cast = (Messages.ClientToClientMessage)message;
                if(cast != null)
                {   
                    ClientToClientCommandReceived?.Invoke(cast);
                }
            }
            else if(msgRealType == typeof(Messages.ServerUpdateQueryMessage))
            {
                m_logger.Debug("Server message is {0}", nameof(Messages.ServerUpdateQueryMessage));
                var cast = (Messages.ServerUpdateQueryMessage)message;
                if(cast != null)
                {
                    ServerAppUpdateRequestReceived?.Invoke(cast);
                }
            }
            else if(msgRealType == typeof(Messages.ServerUpdateNotificationMessage))
            {
                m_logger.Debug("Server message is {0}", nameof(Messages.ServerUpdateNotificationMessage));
                var cast = (Messages.ServerUpdateNotificationMessage)message;
                if(cast != null)
                {
                    ServerUpdateStarting?.Invoke();
                }
            }
            else
            {
                // Unknown type.
            }
        }

        /// <summary>
        /// Requests that the server deactivate the filtering service and shut down.
        /// </summary>
        public void RequestDeactivation()
        {
            var msg = new DeactivationMessage(DeactivationCommand.Requested);
            PushMessage(msg);
        }

        /// <summary>
        /// Sends credentials to the server to attempt authentication. 
        /// </summary>
        /// <param name="username">
        /// Username to authorize with.
        /// </param>
        /// <param name="password">
        /// Password to authorize with.
        /// </param>
        public void AttemptAuthentication(string username, SecureString password)
        {
            var msg = new AuthenticationMessage(AuthenticationAction.Requested, username, password);

            var logger = LoggerUtil.GetAppWideLogger();

            try
            {
                PushMessage(msg);
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(logger, e);
            }
        }

        public void RequestPrimaryClientShowUI()
        {
            var msg = new ClientToClientMessage(ClientToClientCommand.ShowYourself);
            PushMessage(msg);
        }

        public void RequestRelaxedPolicy()
        {
            var msg = new RelaxedPolicyMessage(RelaxedPolicyCommand.Requested);
            PushMessage(msg);
        }

        public void RelinquishRelaxedPolicy()
        {
            var msg = new RelaxedPolicyMessage(RelaxedPolicyCommand.Relinquished);
            PushMessage(msg);
        }

        public void NotifyAcceptUpdateRequest()
        {
            var msg = new ClientUpdateResponseMessage(true);
            PushMessage(msg);
        }

        private void PushMessage(BaseMessage msg)
        {
            var bf = new BinaryFormatter();
            using(var ms = new MemoryStream())
            {
                bf.Serialize(ms, msg);
            }

            m_client.PushMessage(msg);
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
                        m_client.AutoReconnect = false;
                        m_client.Stop();
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
#endregion
    }
}
