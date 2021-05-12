/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;

namespace CloudVeil.IPC.Messages
{

    /// <summary>
    /// Enum of the different commands that a deactivation message can represent. 
    /// </summary>
    [Serializable]
    public enum DeactivationCommand
    {
        /// <summary>
        /// Means that the client has requested that the filter deactivate and shut down.
        /// </summary>
        Requested,

        /// <summary>
        /// Means that the server has denied a request to deactivate and shut down.
        /// </summary>
        Denied,

        /// <summary>
        /// Means that the server has accepted the request to shut down and deactive. Should
        /// be followed by a FilterStatusMessage indicating the shutdown type.
        /// </summary>
        Granted,

        /// <summary>
        /// Means that no response was received from the server.
        /// </summary>
        NoResponse
    }

    /// <summary>
    /// The DeactivationMessage class represents an IPC communication between client (GUI) and server
    /// (Service) with regards to deactivating and shutting down the filter. Both the client and the
    /// server may generate these messages after the user has, via the client (GUI) request a
    /// deactivation. The client and server will negotiate this process back and forth until the
    /// server ultimately makes a decision.
    /// </summary>
    [Serializable]
    public class DeactivationMessage : BaseMessage
    {
        /// <summary>
        /// The command for this message. 
        /// </summary>
        public DeactivationCommand Command
        {
            get;
            private set;
        }

        /// <summary>
        /// Constructs a new DeactivationMessage instance. 
        /// </summary>
        /// <param name="command">
        /// The command this message should carry. 
        /// </param>
        public DeactivationMessage(DeactivationCommand command)
        {
            Command = command;
        }
    }
}