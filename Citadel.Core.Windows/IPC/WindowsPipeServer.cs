/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.IPC.Messages;
using Filter.Platform.Common.IPC;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace CitadelService.Platform
{
    public class WindowsPipeServer : IPipeServer
    {
        private NamedPipeWrapper.NamedPipeServer<BaseMessage> server;

        public WindowsPipeServer(string channel)
        {
            server = new NamedPipeWrapper.NamedPipeServer<BaseMessage>(channel, GetSecurityForChannel());

            server.ClientConnected += (conn) => ClientConnected?.Invoke(this);
            server.ClientDisconnected += clientDisconnected;
            server.ClientMessage += (conn, msg) => ClientMessage?.Invoke(this, msg);
            server.Error += (ex) => Error?.Invoke(ex);
        }

        private void clientDisconnected(NamedPipeWrapper.NamedPipeConnection<BaseMessage, BaseMessage> conn)
        {
            conn.Disconnected -= clientDisconnected;
            ClientDisconnected?.Invoke(this);
        }
        public event ConnectionHandler ClientConnected;
        public event ConnectionHandler ClientDisconnected;
        public event MessageHandler ClientMessage;
        public event PipeExceptionHandler Error;

        public void PushMessage(BaseMessage msg) => server.PushMessage(msg);
        public void Start() => server.Start();
        public void Stop() => server.Stop();

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

            var authUsersSid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            var authUsersAcct = authUsersSid.Translate(typeof(NTAccount));
            pipeSecurity.SetAccessRule(new PipeAccessRule(authUsersAcct, permissions, AccessControlType.Allow));

            return pipeSecurity;
        }
    }
}
