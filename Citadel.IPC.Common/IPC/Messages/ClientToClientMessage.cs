/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;

namespace Citadel.IPC.Messages
{
    /// <summary>
    /// Delegate for the handler of client to client commands. 
    /// </summary>
    /// <param name="msg">
    /// The client to client message.
    /// </param>
    public delegate void ClientToClientMessageHandler(ClientToClientMessage msg);

    /// <summary>
    /// Enum of the different commands that a client to client message can represent. 
    /// </summary>
    [Serializable]
    public enum ClientToClientCommand
    {
        /// <summary>
        /// Means that a client is asking the server to ask other clients to show themselves. 
        /// </summary>
        ShowYourself
    }

    /// <summary>
    /// The ClientToClientMessage class represents an IPC communication between client (GUI) and
    /// other clients (GUI). These messages are relayed through the server to all clients.
    /// </summary>
    [Serializable]
    public class ClientToClientMessage : ClientOnlyMessage
    {
        /// <summary>
        /// The command for this message. 
        /// </summary>
        public ClientToClientCommand Command
        {
            get;
            private set;
        }

        /// <summary>
        /// Constructs a new ClientToClientMessage with the given command. 
        /// </summary>
        /// <param name="command">
        /// The client to client command. 
        /// </param>
        public ClientToClientMessage(ClientToClientCommand command)
        {
            Command = command;
        }
    }
}