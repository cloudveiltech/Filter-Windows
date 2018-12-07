/*
* Copyright � 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using Citadel.IPC.Messages;
using Filter.Platform.Common;
using Filter.Platform.Common.IPC;
using Filter.Platform.Common.Types;
using Filter.Platform.Common.Util;
using NLog;
using System;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Citadel.IPC
{
    /// <summary>
    /// Arguments for the RelaxPolicyRequestHander delegate. 
    /// </summary>
    public class RelaxedPolicyEventArgs : EventArgs
    {
        /// <summary>
        /// The relaxed policy command issued by the client. 
        /// </summary>
        public RelaxedPolicyCommand Command
        {
            get;
            private set;
        }

        /// <summary>
        /// Constructs a new RelaxedPolicyEventArgs from the given client message. 
        /// </summary>
        /// <param name="msg">
        /// The client message. 
        /// </param>
        public RelaxedPolicyEventArgs(RelaxedPolicyMessage msg)
        {
            Command = msg.Command;
        }
    }

    /// <summary>
    /// Delegate for the handler of client relaxed policy messages. 
    /// </summary>
    /// <param name="args">
    /// Relaxed policy request arguments. 
    /// </param>
    public delegate void RelaxPolicyRequestHander(RelaxedPolicyEventArgs args);

    public enum RequestState
    {
        NoResponse = 0,
        Granted,
        Denied
    }

    /// <summary>
    /// Arguments for the DeactivationRequestHandler delegate. 
    /// </summary>
    public class DeactivationRequestEventArgs : EventArgs
    {
        /// <summary>
        /// Whether or not the request was granted. Defaults to false. 
        /// </summary>
        public bool Granted
        {
            get
            {
                return DeactivationCommand == DeactivationCommand.Granted;
            }
        }



        /// <summary>
        /// Did we successfully send the request and get any response back?
        /// </summary>
        public DeactivationCommand DeactivationCommand
        {
            get;
            set;
        }

        /// <summary>
        /// Constructs a new DeactivationRequestEventArgs instance. 
        /// </summary>
        public DeactivationRequestEventArgs()
        {
        }
    }

    /// <summary>
    /// Delegate for the handler of client deactivation requests. 
    /// </summary>
    /// <param name="args">
    /// Deactivation request arguments. 
    /// </param>
    public delegate void DeactivationRequestHandler(DeactivationRequestEventArgs args);

    public class CertificateExemptionEventArgs : EventArgs
    {
        public string Host { get; set; }
        public string CertificateHash { get; set; }

        public bool ExemptionGranted { get; set; }

        public CertificateExemptionEventArgs(CertificateExemptionMessage msg)
        {
            Host = msg.Host;
            CertificateHash = msg.CertificateHash;
            ExemptionGranted = msg.ExemptionGranted;
        }
    }

    public delegate void CertificateExemptionHandler(CertificateExemptionEventArgs args);

    /// <summary>
    /// Arguments for the AuthenticationRequestHandler delegate. 
    /// </summary>
    public class AuthenticationRequestArgs : EventArgs
    {
        /// <summary>
        /// The username with which to attempt authentication. 
        /// </summary>
        public string Username
        {
            get;
            private set;
        }

        /// <summary>
        /// The password with which to attempt authentication. 
        /// </summary>
        public SecureString Password
        {
            get;
            private set;
        }

        /// <summary>
        /// Constructs a new AuthenticationRequestArgs instance from the given client message. 
        /// </summary>
        /// <param name="msg">
        /// The client authentication message. 
        /// </param>
        public AuthenticationRequestArgs(AuthenticationMessage msg)
        {
            Username = msg.Username;
            Password = new SecureString();

            try
            {
                foreach(var c in msg.Password)
                {
                    Password.AppendChar((char)c);
                }
            }
            finally
            {
                Array.Clear(msg.Password, 00, msg.Password.Length);
            }
        }
    }

    /// <summary>
    /// Delegate for the handler of client authentication requests. 
    /// </summary>
    /// <param name="args">
    /// Authentication request arguments. 
    /// </param>
    public delegate void AuthenticationRequestHandler(AuthenticationRequestArgs args);

    /// <summary>
    /// Generic void, parameterless delegate.
    /// </summary>
    public delegate void ServerGenericParameterlessHandler();

    public delegate void RequestConfigUpdateHandler(RequestConfigUpdateMessage message);

    /// <summary>
    /// Handler for requesting a captive portal detection.
    /// </summary>
    /// <param name="message"></param>
    public delegate void RequestCaptivePortalDetectionHandler(CaptivePortalDetectionMessage message);

    public delegate void DiagnosticsEnableHandler(DiagnosticsMessage message);

    /// <summary>
    /// The IPC server class is meant to be used with a session 0 isolated process, more specifically
    /// a Windows service. This class handles requests from clients (GUI) and responds accordingly.
    /// </summary>
    public class IPCServer : IDisposable
    {
        /// <summary>
        /// Actual named pipe server wrapper. 
        /// </summary>
        private IPipeServer m_server;

        // XXX FIXME Currently not used in IPCServer.
        private IPCMessageTracker m_ipcQueue;

        /// <summary>
        /// Delegate to be called when a client requests a relaxed policy. 
        /// </summary>
        public RelaxPolicyRequestHander RelaxedPolicyRequested;

        /// <summary>
        /// Delegate to be called when a client requests filter deactivation. 
        /// </summary>
        public DeactivationRequestHandler DeactivationRequested;

        /// <summary>
        /// Delegate to be called when a client attempts to authenticate. 
        /// </summary>
        public AuthenticationRequestHandler AttemptAuthentication;

        /// <summary>
        /// Delegate to be called when a client connects. 
        /// </summary>
        public ServerGenericParameterlessHandler ClientConnected;

        /// <summary>
        /// Delegate to be called when a client disconnects. 
        /// </summary>
        public ServerGenericParameterlessHandler ClientDisconnected;

        /// <summary>
        /// Delegate to be called when a client is querying the state of the filter. 
        /// </summary>
        public StateChangeHandler ClientServerStateQueried;

        /// <summary>
        /// Delegate to be called when a client has responded accepting a pending
        /// application update.
        /// </summary>
        public ServerGenericParameterlessHandler ClientAcceptedPendingUpdate;

        /// <summary>
        /// Delegate to be called when a client has submitted a block action for
        /// review.
        /// </summary>
        public BlockActionReportHandler ClientRequestsBlockActionReview;

        /// <summary>
        /// Delegate to be called when a client is requesting a configuration/ruleset update.
        /// </summary>
        public RequestConfigUpdateHandler RequestConfigUpdate;

        /// <summary>
        /// Delegate to be called when a client is requesting a captive portal state.
        /// </summary>
        public RequestCaptivePortalDetectionHandler RequestCaptivePortalDetection;

        /// <summary>
        /// Delegate to be called when a client grants a certificate exemption.
        /// </summary>
        public CertificateExemptionHandler OnCertificateExemptionGranted;

        /// <summary>
        /// Delegate to be called when a client enables diagnostics information.
        /// </summary>
        public DiagnosticsEnableHandler OnDiagnosticsEnable;

        /// <summary>
        /// Our logger. 
        /// </summary>
        private readonly Logger m_logger;

        public bool WaitingForAuth
        {
            get
            {
                return m_waitingForAuth;
            }
        }

        private volatile bool m_waitingForAuth = false;

        /// <summary>
        /// Constructs a new named pipe server for IPC, with a channel name derived from the class
        /// namespace and the current machine's digital fingerprint.
        /// </summary>
        public IPCServer()
        {
            m_logger = LoggerUtil.GetAppWideLogger();

            var channel = string.Format("{0}.{1}", nameof(Citadel.IPC), FingerprintService.Default.Value).ToLower();

            m_server = PlatformTypes.New<IPipeServer>(channel);

            //m_server = new NamedPipeServer<BaseMessage>(channel, security);
            
            m_server.ClientConnected += OnClientConnected;
            m_server.ClientDisconnected += OnClientDisconnected;
            m_server.ClientMessage += OnClientMessage;

            m_server.Error += M_server_Error;

            // Server is no longer started by constructor. We start the IPCServer after everything else has been set up by the FilterServiceProvider.
            m_ipcQueue = new IPCMessageTracker();
            
        }

        private void M_server_Error(Exception exception)
        {
            LoggerUtil.RecursivelyLogException(m_logger, exception);
        }

        private void OnClientConnected(IPipeServer server)
        {
            m_logger.Debug("Client connected.");
            ClientConnected?.Invoke();
        }

        private void OnClientDisconnected(IPipeServer server)
        {
            m_logger.Debug("Client disconnected.");
            ClientDisconnected?.Invoke();
        }

        /// <summary>
        /// Handles a received client message. 
        /// </summary>
        /// <param name="connection">
        /// The connection over which the message was received. 
        /// </param>
        /// <param name="message">
        /// The client's message to us. 
        /// </param>
        private void OnClientMessage(IPipeServer server, BaseMessage message)
        {
            // This is so gross, but unfortuantely we can't just switch on a type. We can come up
            // with a nice mapping system so we can do a switch, but this can wait.

            m_logger.Debug("Got IPC message from client.");

            var msgRealType = message.GetType();

            if(msgRealType == typeof(Messages.AuthenticationMessage))
            {
                m_logger.Debug("Client message is {0}", nameof(Messages.AuthenticationMessage));
                var cast = (Messages.AuthenticationMessage)message;

                if(cast != null)
                {
                    if(string.IsNullOrEmpty(cast.Username) || string.IsNullOrWhiteSpace(cast.Username))
                    {
                        PushMessage(new AuthenticationMessage(AuthenticationAction.InvalidInput));
                        return;
                    }

                    if(cast.Password == null || cast.Password.Length <= 0)
                    {
                        PushMessage(new AuthenticationMessage(AuthenticationAction.InvalidInput));
                        return;
                    }

                    var args = new AuthenticationRequestArgs(cast);

                    AttemptAuthentication?.Invoke(args);
                }
            }
            else if(msgRealType == typeof(Messages.DeactivationMessage))
            {
                m_logger.Debug("Client message is {0}", nameof(Messages.DeactivationMessage));

                var cast = (Messages.DeactivationMessage)message;

                if(cast != null && cast.Command == DeactivationCommand.Requested)
                {
                    var args = new DeactivationRequestEventArgs();
                    
                    // This fills args.DeactivationCommand.
                    DeactivationRequested?.Invoke(args);

                    PushMessage(new DeactivationMessage(args.DeactivationCommand));
                }
            }
            else if(msgRealType == typeof(Messages.RelaxedPolicyMessage))
            {
                m_logger.Debug("Client message is {0}", nameof(Messages.RelaxedPolicyMessage));

                var cast = (Messages.RelaxedPolicyMessage)message;

                if(cast != null)
                {
                    var args = new RelaxedPolicyEventArgs(cast);
                    RelaxedPolicyRequested?.Invoke(args);
                }
            }
            else if(msgRealType == typeof(Messages.ClientToClientMessage))
            {
                m_logger.Debug("Client message is {0}", nameof(Messages.ClientToClientMessage));

                var cast = (Messages.ClientToClientMessage)message;

                if(cast != null)
                {
                    // Just relay this message to all clients.
                    PushMessage(cast);
                }
            }
            else if(msgRealType == typeof(Messages.FilterStatusMessage))
            {
                m_logger.Debug("Client message is {0}", nameof(Messages.FilterStatusMessage));

                var cast = (Messages.FilterStatusMessage)message;

                if(cast != null)
                {
                    ClientServerStateQueried?.Invoke(new StateChangeEventArgs(cast));
                }
            }
            else if(msgRealType == typeof(Messages.ClientUpdateResponseMessage))
            {
                m_logger.Debug("Client message is {0}", nameof(Messages.ClientUpdateResponseMessage));

                var cast = (Messages.ClientUpdateResponseMessage)message;

                if(cast != null)
                {
                    if(cast.Accepted)
                    {
                        m_logger.Debug("Client has accepted update.");
                        ClientAcceptedPendingUpdate?.Invoke();
                    }                    
                }
            }
            else if(msgRealType == typeof(Messages.BlockActionReviewRequestMessage))
            {
                m_logger.Debug("Client message is {0}", nameof(Messages.BlockActionReviewRequestMessage));

                var cast = (Messages.BlockActionReviewRequestMessage)message;

                if(cast != null)
                {
                    Uri output;
                    if(Uri.TryCreate(cast.FullRequestUrl, UriKind.Absolute, out output))
                    {
                        // Here we'll just recycle the block action message and handler.
                        ClientRequestsBlockActionReview?.Invoke(new NotifyBlockActionMessage(BlockType.OtherContentClassification, output, string.Empty, cast.CategoryName));
                    }
                    else
                    {
                        m_logger.Info("Failed to create absolute URI for string \"{0}\".", cast.FullRequestUrl);
                    }                    
                }
            }
            else if(msgRealType == typeof(Messages.RequestConfigUpdateMessage))
            {
                m_logger.Debug("Client message is {0}", nameof(Messages.RequestConfigUpdateMessage));

                var cast = (Messages.RequestConfigUpdateMessage)message;

                if(cast != null)
                {
                    RequestConfigUpdate?.Invoke(cast);
                }
            }
            else if (msgRealType == typeof(Messages.CaptivePortalDetectionMessage))
            {
                m_logger.Debug("Server message is {0}", nameof(Messages.CaptivePortalDetectionMessage));
                var cast = (Messages.CaptivePortalDetectionMessage)message;
                if (cast != null)
                {
                    RequestCaptivePortalDetection?.Invoke(cast);
                }
            }
            else if(msgRealType == typeof(Messages.CertificateExemptionMessage))
            {
                m_logger.Debug("Server message is {0}", nameof(Messages.CertificateExemptionMessage));
                var cast = (Messages.CertificateExemptionMessage)message;
                if(cast != null)
                {
                    this.OnCertificateExemptionGranted?.Invoke(new CertificateExemptionEventArgs(cast));
                }
            }
            else if (msgRealType == typeof(Messages.DiagnosticsMessage))
            {
                m_logger.Debug("Server message is {0}", nameof(Messages.DiagnosticsMessage));
                var cast = (Messages.DiagnosticsMessage)message;
                if (cast != null)
                {
                    this.OnDiagnosticsEnable?.Invoke(cast);
                }
            }
            else
            {
                // Unknown type.
            }
        }

        public void Start()
        {
            m_logger.Info("IPC Server Started");
            m_server.Start();
        }

        /// <summary>
        /// Notifies all clients of the supplied relaxed policy.Debugrmation. 
        /// </summary>
        /// <param name="numPoliciesAvailable">
        /// The number of relaxed policy uses available. 
        /// </param>
        /// <param name="policyDuration">
        /// The duration of the relaxed policies. 
        /// </param>
        /// <param name="isActive">
        /// Whether or not the relaxed policy is currently active.
        /// </param>
        /// <param name="command">
        /// The relaxed policy command which caused this notification to happen.
        /// If == RelaxedPolicyCommand.Info, ignore.
        /// </param>
        public void NotifyRelaxedPolicyChange(int numPoliciesAvailable, TimeSpan policyDuration, RelaxedPolicyStatus status)
        {
            var nfo = new RelaxedPolicyInfo(policyDuration, numPoliciesAvailable, status);
            var msg = new RelaxedPolicyMessage(RelaxedPolicyCommand.Info, nfo);
            PushMessage(msg);
        }

        /// <summary>
        /// Notifies clients of the supplied status change. 
        /// </summary>
        /// <param name="status">
        /// The status to send to all clients. 
        /// </param>
        public void NotifyStatus(FilterStatus status)
        {   
            var msg = new FilterStatusMessage(status);
            PushMessage(msg);
        }

        public void NotifyCooldownEnforced(TimeSpan cooldownPeriod)
        {
            var msg = new FilterStatusMessage(cooldownPeriod);
            PushMessage(msg);
        }

        /// <summary>
        /// Notifies clients of a block action. 
        /// </summary>
        /// <param name="type">
        /// The type of block action, AKA the cause. 
        /// </param>
        /// <param name="resource">
        /// The absolute URI of the requested resource that caused the blocked network connection. 
        /// </param>
        /// <param name="category">
        /// The category that the matching rule belongs to. 
        /// </param>
        /// <param name="rule">
        /// The matching rule, if applicable. Defaults to null; 
        /// </param>
        public void NotifyBlockAction(BlockType type, Uri resource, string category, string rule = null)
        {
            var msg = new NotifyBlockActionMessage(type, resource, rule, category);
            PushMessage(msg);
        }

        /// <summary>
        /// Notifies clients of the current authentication state. 
        /// </summary>
        /// <param name="action">
        /// The authentication command which reflects the current auth state. 
        /// </param>
        public void NotifyAuthenticationStatus(AuthenticationAction action, string username = null, AuthenticationResultObject authenticationResult = null)
        {
            // KF - I edited this function to take two arguments instead of one and then refactored all the code that calls it to pass in an AuthenticationResultObject
            switch (m_waitingForAuth)
            {
                case true:
                {
                    m_waitingForAuth = action == AuthenticationAction.Authenticated ? false : true;
                }
                break;

                case false:
                {
                    m_waitingForAuth = action == AuthenticationAction.Required;
                }
                break;
            }


            var authResult = new AuthenticationResultObject();

            if(authenticationResult != null)
            {
                authResult = authenticationResult;
            }

            var msg = new AuthenticationMessage(action, authResult, username); // KF - Also added a constructor to AuthenticationMessage);

            PushMessage(msg);
        }

        /// <summary>
        /// Notifies all clients of an available update. 
        /// </summary>
        /// <param name="message">
        /// The update message.
        /// </param>
        public void NotifyApplicationUpdateAvailable(ServerUpdateQueryMessage message)
        {
            PushMessage(message);
        }

        /// <summary>
        /// Notifies all clients that the server is updating itself.
        /// </summary>
        public void NotifyUpdating()
        {
            var msg = new ServerUpdateNotificationMessage();
            PushMessage(msg);
        }

        public void NotifyConfigurationUpdate(ConfigUpdateResult result, Guid replyToId)
        {
            var msg = new NotifyConfigUpdateMessage(result);
            msg.ReplyToId = replyToId;
            PushMessage(msg);
        }

        public void NotifyAddCertificateExemption(string host, string certHash, bool isTrusted)
        {
            m_logger.Info("Sending certificate exemption");

            var msg = new CertificateExemptionMessage(host, certHash, isTrusted);
            PushMessage(msg);
        }

        /// <summary>
        /// Send captive portal state back to the client.
        /// </summary>
        /// <param name="captivePortalState"></param>
        public void SendCaptivePortalState(bool captivePortalState, bool isCaptivePortalActive)
        {
            var msg = new CaptivePortalDetectionMessage(captivePortalState, isCaptivePortalActive);
            PushMessage(msg);
        }

        public void SendDiagnosticsInfo(DiagnosticsInfoV1 info)
        {
            var message = new DiagnosticsInfoMessage();
            message.ObjectVersion = DiagnosticsVersion.V1;
            message.Info = info;

            PushMessage(message);
        }

        public void SendConfigurationInfo(ConfigurationInfoMessage msg)
        {
            PushMessage(msg);
        }

        private void PushMessage(BaseMessage msg)
        {
            if(m_waitingForAuth)
            {
                // We'll only allow auth messages through until authentication has
                // been confirmed.
                if(msg.GetType() == typeof(AuthenticationMessage))
                {
                    m_server.PushMessage(msg);
                }
                else if(msg.GetType() == typeof(RelaxedPolicyMessage))
                {
                    m_server.PushMessage(msg);
                }
                else if(msg.GetType() == typeof(NotifyBlockActionMessage))
                {
                    m_server.PushMessage(msg);
                }
            }
            else
            {
                m_server.PushMessage(msg);
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
                    if(m_server != null)
                    {
#if CLIFTON
                        m_server.Close();
#else
                        m_server.Stop();
#endif
                        m_server = null;
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free
        //       unmanaged resources. ~IPCServer() { // Do not change this code. Put cleanup code in
        // Dispose(bool disposing) above. Dispose(false); }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above. GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}