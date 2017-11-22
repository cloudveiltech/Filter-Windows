/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;

namespace Citadel.IPC.Messages
{
    /// <summary>
    /// The ClientUpdateResponseMessage class represents an IPC communication between client (GUI)
    /// and server (Service) about application updates. This message is sent exclusively by the
    /// client with a boolean response about accepting an application update. This should only be
    /// used in response to an update request sent from the server.
    /// </summary>
    [Serializable]
    public class ClientUpdateResponseMessage : ClientOnlyMessage
    {
        /// <summary>
        /// Whether or not the client has accepted the previous solicitation to update. 
        /// </summary>
        public bool Accepted
        {
            get;
            private set;
        }

        /// <summary>
        /// Constructs a new ClientUpdateResponseMessage instance. 
        /// </summary>
        /// <param name="accepted">
        /// Whether or not the client has accepted the previous solicitation to update. 
        /// </param>
        public ClientUpdateResponseMessage(bool accepted)
        {
            Accepted = accepted;
        }
    }
}