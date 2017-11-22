/*
* Copyright © 2017 Cloudveil Technology Inc.  
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

    public delegate void AuthenticationResultHandler(AuthenticationMessage result);

    public delegate void DeactivationRequestResultHandler(bool granted);

    public delegate AuthenticationMessage GetAuthMessage();

    public delegate void BlockActionReportHandler(NotifyBlockActionMessage msg);

    public delegate void RelaxedPolicyInfoReceivedHandler(RelaxedPolicyMessage msg);

    public delegate void ClientGenericParameterlessHandler();

    public delegate void ServerUpdateRequestHandler(ServerUpdateQueryMessage msg);

    /// <summary>
    /// A generic reply handler, called by IPC queue.
    /// </summary>
    /// <param name="msg"></param>
    /// <returns></returns>
    public delegate bool GenericReplyHandler(BaseMessage msg);

    public class IPCClient : IDisposable
    {

        private NamedPipeClient<BaseMessage> m_client;

        private IPCMessageTracker m_ipcQueue;

        public ClientGenericParameterlessHandler ConnectedToServer;

        public ClientGenericParameterlessHandler DisconnectedFromServer;

        public StateChangeHandler StateChanged;

        public AuthenticationResultHandler AuthenticationResultReceived;

        private AuthenticationMessage AuthMessage;

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

        /// <summary>
        /// All message handlers get added to this IPC client as it will stick around and handle all the incoming messages.
        /// </summary>
        public static IPCClient Default { get; set; }

        public static IPCClient InitDefault()
        {
            Default = new IPCClient(true);
            return Default;
        }

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

            m_ipcQueue = new IPCMessageTracker();
        }

        public void WaitForConnection()
        {
            m_client.WaitForConnection();
        }

        public AuthenticationMessage GetAuthMessage()
        {
            return AuthMessage;
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

            if(Default.m_ipcQueue.HandleMessage(message))
            {
                return;
            }

            if(m_ipcQueue.HandleMessage(message))
            {
                return;
            }

            m_logger.Debug("Got IPC message from server.");

            var msgRealType = message.GetType();

            if(msgRealType == typeof(AuthenticationMessage))
            {
                AuthMessage = (AuthenticationMessage)message;
                m_logger.Debug("Server message is {0}", nameof(AuthenticationMessage));
                var cast = (AuthenticationMessage)message;
                if(cast != null)
                {   
                    AuthenticationResultReceived?.Invoke(cast);
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
        /// Sends an IPC message to the server notifying that the client has requested a block action
        /// to be reviewed.
        /// </summary>
        /// <param name="category">
        /// The category of the rule that caused the block action.
        /// </param>
        /// <param name="fullRequestUrl">
        /// The full URL that was blocked by the rule.
        /// </param>
        public void RequestBlockActionReview(string category, string fullRequestUrl)
        {
            var msg = new BlockActionReviewRequestMessage(category, fullRequestUrl);
            PushMessage(msg);
        }

        /// <summary>
        /// Requests the current status from the IPC server.
        /// </summary>
        public void RequestStatusRefresh()
        {
            var msg = new FilterStatusMessage(FilterStatus.Query);
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

        public void RequestConfigUpdate(Action<NotifyConfigUpdateMessage> replyHandler)
        {
            var msg = new RequestConfigUpdateMessage();
            PushMessage(msg, (reply) =>
            {
                if (reply.GetType() == typeof(Messages.NotifyConfigUpdateMessage))
                {
                    replyHandler((Messages.NotifyConfigUpdateMessage)reply);
                    return true;
                }
                else
                {
                    return false;
                }
            });
        }

        private void PushMessage(BaseMessage msg, GenericReplyHandler replyHandler = null)
        {
            var bf = new BinaryFormatter();
            using(var ms = new MemoryStream())
            {
                bf.Serialize(ms, msg);
            }

            m_client.PushMessage(msg);
            
            if(replyHandler != null)
            {
                if (Default != null)
                {
                    Default.m_ipcQueue.AddMessage(msg, replyHandler);
                }
                else
                {
                    m_ipcQueue.AddMessage(msg, replyHandler);
                }
            }
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
