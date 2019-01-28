/*
* Copyright � 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using Citadel.IPC.Messages;
using Filter.Platform.Common;
using Filter.Platform.Common.IPC;
using Filter.Platform.Common.Util;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    public delegate void DeactivationRequestResultHandler(DeactivationCommand args);

    public delegate AuthenticationMessage GetAuthMessage();

    public delegate void BlockActionReportHandler(NotifyBlockActionMessage msg);

    public delegate void RelaxedPolicyInfoReceivedHandler(RelaxedPolicyMessage msg);

    public delegate void ClientGenericParameterlessHandler();

    public delegate void ServerUpdateRequestHandler(ServerUpdateQueryMessage msg);

    public delegate void CaptivePortalDetectionHandler(CaptivePortalDetectionMessage msg);

    public delegate void AddCertificateExemptionRequestHandler(CertificateExemptionMessage msg);

    public delegate void DiagnosticsInfoHandler(DiagnosticsInfoMessage msg);

    /// <summary>
    /// A generic reply handler, called by IPC queue.
    /// </summary>
    /// <param name="msg"></param>
    /// <returns></returns>
    public delegate bool GenericReplyHandler(BaseMessage msg);

    public class IPCClient : IpcCommunicator, IDisposable
    {
        private IPipeClient client;

        protected IPCMessageTracker ipcQueue;

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

        public CaptivePortalDetectionHandler CaptivePortalDetectionReceived;

        public ClientGenericParameterlessHandler ServerUpdateStarting;

        public AddCertificateExemptionRequestHandler AddCertificateExemptionRequest;

        public DiagnosticsInfoHandler OnDiagnosticsInfo;

        /// <summary>
        /// Our logger.
        /// </summary>
        protected readonly Logger logger;

        /// <summary>
        /// All message handlers get added to this IPC client as it will stick around and handle all the incoming messages.
        /// </summary>
        private static IPCClient s_default;
        public static IPCClient Default
        {
            get
            {
                if(s_default == null)
                {
                    throw new NullReferenceException("You must specify a Default IPCClient before using it.");
                }

                return s_default;
            }

            set
            {
                s_default = value;
            }
        }

        static IPCClient()
        {
        }

        public static IPCClient InitDefault()
        {
            Default = new IPCClient(true);
            return Default;
        }

        public IPCClient(bool autoReconnect = false)
        {
            logger = LoggerUtil.GetAppWideLogger();
            ipcQueue = new IPCMessageTracker();

            var channel = string.Format("{0}.{1}", nameof(Citadel.IPC), FingerprintService.Default.Value).ToLower();

            client = PlatformTypes.New<IPipeClient>(channel, autoReconnect); // new NamedPipeClient<BaseMessage>(channel);

            logger.Info("Process {0} creating client", Process.GetCurrentProcess().Id);

            client.Connected += OnConnected;
            client.Disconnected += OnDisconnected;
            client.ServerMessage += OnServerMessage;
            client.AutoReconnect = autoReconnect;

            client.Error += clientError;

            client.Start();

            m_callbacks.Add(typeof(IpcMessage), (msg) =>
            {
                HandleIpcMessage(msg as IpcMessage);
            });
        }

        public AuthenticationMessage GetAuthMessage()
        {
            return AuthMessage;
        }

        protected void OnConnected()
        {
            ConnectedToServer?.Invoke();
        }

        protected void OnDisconnected()
        {
            DisconnectedFromServer?.Invoke();
        }

        private void clientError(Exception ex)
        {
            LoggerUtil.RecursivelyLogException(logger, ex);
        }

        public void WaitForConnection()
        {
            client.WaitForConnection();
        }

        private Dictionary<Type, Action<BaseMessage>> m_callbacks = new Dictionary<Type, Action<BaseMessage>>();

        protected void OnServerMessage(BaseMessage message)
        {
            // This is so gross, but unfortuantely we can't just switch on a type.
            // We can come up with a nice mapping system so we can do a switch,
            // but this can wait.

            if(Default.ipcQueue.HandleMessage(message))
            {
                return;
            }

            if(ipcQueue.HandleMessage(message))
            {
                return;
            }

            logger.Debug("Got IPC message from server.");

            var msgRealType = message.GetType();

            Action<BaseMessage> callback = null;
            if(m_callbacks.TryGetValue(msgRealType, out callback))
            {
                logger.Debug("Server message is {0}", msgRealType.Name);
                callback?.Invoke(message);
            }
            else if(msgRealType == typeof(AuthenticationMessage))
            {
                AuthMessage = (AuthenticationMessage)message;
                logger.Debug("Server message is {0}", nameof(AuthenticationMessage));
                var cast = (AuthenticationMessage)message;
                if(cast != null)
                {   
                    AuthenticationResultReceived?.Invoke(cast);
                }
            }
            else if(msgRealType == typeof(Messages.DeactivationMessage))
            {
                logger.Debug("Server message is {0}", nameof(Messages.DeactivationMessage));
                var cast = (Messages.DeactivationMessage)message;
                if(cast != null)
                {   
                    DeactivationResultReceived?.Invoke(cast.Command);
                }
            }
            else if(msgRealType == typeof(Messages.FilterStatusMessage))
            {
                logger.Debug("Server message is {0}", nameof(Messages.FilterStatusMessage));
                var cast = (Messages.FilterStatusMessage)message;
                if(cast != null)
                {   
                    StateChanged?.Invoke(new StateChangeEventArgs(cast));
                }
            }
            else if(msgRealType == typeof(Messages.NotifyBlockActionMessage))
            {
                logger.Debug("Server message is {0}", nameof(Messages.NotifyBlockActionMessage));
                var cast = (Messages.NotifyBlockActionMessage)message;
                if(cast != null)
                {   
                    BlockActionReceived?.Invoke(cast);
                }
            }
            else if(msgRealType == typeof(Messages.RelaxedPolicyMessage))
            {
                logger.Debug("Server message is {0}", nameof(Messages.RelaxedPolicyMessage));
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
                logger.Debug("Server message is {0}", nameof(Messages.ClientToClientMessage));
                var cast = (Messages.ClientToClientMessage)message;
                if(cast != null)
                {   
                    ClientToClientCommandReceived?.Invoke(cast);
                }
            }
            else if(msgRealType == typeof(Messages.ServerUpdateQueryMessage))
            {
                logger.Debug("Server message is {0}", nameof(Messages.ServerUpdateQueryMessage));
                var cast = (Messages.ServerUpdateQueryMessage)message;
                if(cast != null)
                {
                    ServerAppUpdateRequestReceived?.Invoke(cast);
                }
            }
            else if(msgRealType == typeof(Messages.ServerUpdateNotificationMessage))
            {
                logger.Debug("Server message is {0}", nameof(Messages.ServerUpdateNotificationMessage));
                var cast = (Messages.ServerUpdateNotificationMessage)message;
                if(cast != null)
                {
                    ServerUpdateStarting?.Invoke();
                }
            }
            else if(msgRealType == typeof(Messages.CaptivePortalDetectionMessage))
            {
                logger.Debug("Server message is {0}", nameof(Messages.CaptivePortalDetectionMessage));
                var cast = (Messages.CaptivePortalDetectionMessage)message;
                if(cast != null)
                {
                    CaptivePortalDetectionReceived?.Invoke(cast);
                }
            }
            else if(msgRealType == typeof(Messages.CertificateExemptionMessage))
            {
                logger.Debug("Server message is {0}", nameof(Messages.CertificateExemptionMessage));
                var cast = (Messages.CertificateExemptionMessage)message;
                if(cast != null)
                {
                    AddCertificateExemptionRequest?.Invoke(cast);
                }
            }
            else if(msgRealType == typeof(Messages.DiagnosticsInfoMessage))
            {
                logger.Debug("Server message is {0}", nameof(Messages.DiagnosticsInfoMessage));
                var cast = (Messages.DiagnosticsInfoMessage)message;
                if(cast != null)
                {
                    OnDiagnosticsInfo?.Invoke(cast);
                }
            }
            else
            {
                // Unknown type.
                logger.Info("Unknown type is {0}", msgRealType.Name);
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

        public ReplyHandlerClass RequestAddSelfModeratedSite(string site)
        {
            return Request(IpcCall.AddSelfModeratedSite, site);
        }

        public void TrustCertificate(string host, string certificateHash)
        {
            var msg = new CertificateExemptionMessage(host, certificateHash, true);
            PushMessage(msg);
        }

        public void SendDiagnosticsEnable(bool enable)
        {
            var msg = new DiagnosticsMessage();
            msg.EnableDiagnostics = enable;
            PushMessage(msg);
        }

        public override ReplyHandlerClass Request(IpcCall call, object data = null, BaseMessage replyToThis = null)
        {
            ReplyHandlerClass h = new ReplyHandlerClass(this);

            BaseMessage msg = IpcMessage.Request(call, data);
            msg.ReplyToId = replyToThis?.Id ?? Guid.Empty;

            PushMessage(msg, h.TriggerHandler);
            return h;
        }

        public override ReplyHandlerClass Send(IpcCall call, object data, BaseMessage replyToThis = null)
        {
            ReplyHandlerClass h = new ReplyHandlerClass(this);

            BaseMessage msg = IpcMessage.Send(call, data);
            msg.ReplyToId = replyToThis?.Id ?? Guid.Empty;

            PushMessage(msg, h.TriggerHandler);
            return h;
        }

        protected void PushMessage(BaseMessage msg, GenericReplyHandler replyHandler = null)
        {
            var bf = new BinaryFormatter();
            using(var ms = new MemoryStream())
            {
                bf.Serialize(ms, msg);
            }

            client.PushMessage(msg);
            
            if(replyHandler != null)
            {
                if (Default != null)
                {
                    Default.ipcQueue.AddMessage(msg, replyHandler);
                }
                else
                {
                    ipcQueue.AddMessage(msg, replyHandler);
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
                    client.AutoReconnect = false;
                    client.Stop();
                    client = null;
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
