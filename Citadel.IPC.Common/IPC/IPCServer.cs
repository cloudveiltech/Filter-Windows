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
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
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
            get;
            set;
        } = false;

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
    /// Delegate for the handler of client connection and disconnection.
    /// </summary>
    public delegate void ClientConnectDisconnectHandler();

    /// <summary>
    /// The IPC server class is meant to be used with a session 0 isolated process, more specifically
    /// a Windows service. This class handles requests from clients (GUI) and responds accordingly.
    /// </summary>
    public class IPCServer : IDisposable
    {

        /// <summary>
        /// Actual named pipe server wrapper.
        /// </summary>
        private NamedPipeServer<BaseMessage> m_server;

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
        public ClientConnectDisconnectHandler ClientConnected;

        /// <summary>
        /// Delegate to be called when a client disconnects.
        /// </summary>
        public ClientConnectDisconnectHandler ClientDisconnected;

        /// <summary>
        /// Delegate to be called when a client is querying the state of the filter.
        /// </summary>
        public StateChangeHandler ClientServerStateQueried;

        /// <summary>
        /// Our logger.
        /// </summary>
        private readonly Logger m_logger;

        /// <summary>
        /// Constructs a new named pipe server for IPC, with a channel name derived from the class
        /// namespace and the current machine's digital fingerprint.
        /// </summary>
        public IPCServer()
        {
            m_logger = LoggerUtil.GetAppWideLogger();

            var channel = string.Format("{0}.{1}", nameof(Citadel.IPC), FingerPrint.Value).ToLower();

            // Not necessary. Leaving in case.
            // var security = GetSecurityForChannel();

            m_server = new NamedPipeServer<BaseMessage>(channel);//, security);

            m_server.ClientConnected += OnClientConnected;
            m_server.ClientDisconnected += OnClientDisconnected;
            m_server.ClientMessage += OnClientMessage;

            m_server.Error += M_server_Error;

            m_server.Start();

            m_logger.Info("IPC Server started.");
        }

        private void M_server_Error(Exception exception)
        {   
            LoggerUtil.RecursivelyLogException(m_logger, exception);
        }

        private void OnClientConnected(NamedPipeConnection<BaseMessage, BaseMessage> connection)
        {   
            m_logger.Debug("Client connected.");
            ClientConnected?.Invoke();
        }

        private void OnClientDisconnected(NamedPipeConnection<BaseMessage, BaseMessage> connection)
        {
            m_logger.Debug("Client disconnected.");
            ClientDisconnected?.Invoke();
            connection.ReceiveMessage -= OnClientMessage;
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
        private void OnClientMessage(NamedPipeConnection<BaseMessage, BaseMessage> connection, BaseMessage message)
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
                    DeactivationRequested?.Invoke(args);

                    switch(args.Granted)
                    {
                        case true:
                        {
                            PushMessage(new DeactivationMessage(DeactivationCommand.Granted));
                        }
                        break;

                        case false:
                        {
                            PushMessage(new DeactivationMessage(DeactivationCommand.Denied));
                        }
                        break;
                    }
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
            else
            {
                // Unknown type.
            }
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
        public void NotifyRelaxedPolicyChange(int numPoliciesAvailable, TimeSpan policyDuration)
        {
            var nfo = new RelaxedPolicyInfo(policyDuration, numPoliciesAvailable);
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
        public void NotifyAuthenticationStatus(AuthenticationAction action)
        {
            var msg = new AuthenticationMessage(action);
            PushMessage(msg);
        }

        private void PushMessage(BaseMessage msg)
        {
#if CLIFTON
            var bf = new BinaryFormatter();
            using(var ms = new MemoryStream())
            {
                bf.Serialize(ms, msg);
                m_server.WriteBytes(ms.ToArray()).Wait();
                m_server.Flush();
            }
#else
            m_server.PushMessage(msg);
#endif
        }

        /// <summary>
        /// Gets a security descriptor that will permit non-elevated clients to connect to the server. 
        /// </summary>
        /// <returns>
        /// A security descriptor that will permit non-elevated clients to connect to the server. 
        /// </returns>
        private static PipeSecurity GetSecurityForChannel()
        {
            PipeSecurity pipeSecurity = new PipeSecurity();


            var permissions = PipeAccessRights.CreateNewInstance | PipeAccessRights.Read | PipeAccessRights.Synchronize | PipeAccessRights.Write;
            
            
            /*
            ps.AddAccessRule(new PipeAccessRule("Users", PipeAccessRights.ReadWrite, AccessControlType.Allow));
            ps.AddAccessRule(new PipeAccessRule("SYSTEM", PipeAccessRights.FullControl, AccessControlType.Allow));

            return ps;
            */

            /*
            ps.AddAccessRule(new PipeAccessRule("Users", PipeAccessRights.ReadWrite, AccessControlType.Allow));
            ps.AddAccessRule(new PipeAccessRule(System.Security.Principal.WindowsIdentity.GetCurrent().Name, PipeAccessRights.FullControl, AccessControlType.Allow));
            ps.AddAccessRule(new PipeAccessRule("SYSTEM", PipeAccessRights.FullControl, AccessControlType.Allow));
            ps.AddAccessRule(pa);
            */

            
            var everyoneSid = new SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid, null);
            var everyoneAcct = everyoneSid.Translate(typeof(NTAccount));

            var par = new PipeAccessRule(everyoneAcct, permissions, AccessControlType.Allow);
            pipeSecurity.AddAccessRule(par);
            pipeSecurity.SetAccessRule(par);

            
            var authUsersSid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            var authUsersAcct = authUsersSid.Translate(typeof(NTAccount));

            pipeSecurity.AddAccessRule(new PipeAccessRule(authUsersAcct, permissions, AccessControlType.Allow));
            pipeSecurity.SetAccessRule(new PipeAccessRule(authUsersAcct, permissions, AccessControlType.Allow));

            return pipeSecurity;

            /*
            // Allow Everyone read and write access to the pipe.
            pipeSecurity.SetAccessRule(new PipeAccessRule(
                "Authenticated Users",
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                PipeAccessRights.ReadWrite, AccessControlType.Allow));

            // Allow the Administrators group full access to the pipe.
            pipeSecurity.SetAccessRule(new PipeAccessRule(
                "Administrators",
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                PipeAccessRights.FullControl, AccessControlType.Allow));
            */


            /*
            // XXX TODO - We MIGHT want to tighten this up later. However, we want to ensure that
            // regardless of the privilege level of the client (GUI), we can connect to the channel.
            // A much BETTER option is to pull in Bouncy Castle Portable and write a pipe wrapper
            // that encrypts the channel. This way we can embed credentials in the client and server
            // and pin those credentials in order to seal tight this potential attack vector.
            //
            // In our current state, we're not too worried about people reading the pipe data. In the
            // future this could become a threat if we become dependent on anything exchanged over
            // IPC. At present time, the server (process) ultimately makes all the decisions based on
            // the data from the upstream remote service provider so it's not like someone can make a
            // tool to tell the server, over this channel to quit.
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
            sec.SetAccessRule(new PipeAccessRule(everyone, PipeAccessRights.FullControl, AccessControlType.Allow));
            sec.SetAccessRule(new PipeAccessRule(users, PipeAccessRights.FullControl, AccessControlType.Allow));

            return sec;
            */
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