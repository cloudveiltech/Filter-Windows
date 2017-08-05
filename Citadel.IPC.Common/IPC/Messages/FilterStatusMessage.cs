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
    /// Enum of the different status states that a filter status message can represent. 
    /// </summary>
    [Serializable]
    public enum FilterStatus
    {
        /// <summary>
        /// Means that the client is querying the state of the service.
        /// </summary>
        Query,

        /// <summary>
        /// Status indicates that the filter is up and running. 
        /// </summary>
        Running,

        /// <summary>
        /// Status indicates that the filter is awaiting client authentication.
        /// </summary>
        AwaitingCredentials,

        /// <summary>
        /// Status indicates that the server is in the process of fetching data from the remote
        /// upstream provider.
        /// </summary>
        Synchronizing,

        /// <summary>
        /// Means that the filter has disabled all internet, because block action threshold detection
        /// is enabled in the client configuration, and this has been met or exceeded.
        /// </summary>
        CooldownPeriodEnforced
    }

    /// <summary>
    /// The FilterStatusMessage class represents an IPC communication between client (GUI) and server
    /// (Service) about the current state of the server. This message type should never be used by
    /// the client. If the client attempts to construct an instance of this class, the constructor
    /// will throw.
    /// </summary>
    [Serializable]
    public class FilterStatusMessage : ServerOnlyMessage
    {
        /// <summary>
        /// The status of the filter. 
        /// </summary>
        public FilterStatus Status
        {
            get;
            private set;
        }

        /// <summary>
        /// The cooldown duration. This is only relevant when the Status is set to Cooldown. 
        /// </summary>
        public TimeSpan CooldownDuration
        {
            get;
            private set;
        }

        /// <summary>
        /// Constructs a new FilterStatusMessage instance. Do not use this constructor for cooldown
        /// status. Use the constructor that accepts a TimeSpan parameter.
        /// </summary>
        /// <param name="status">
        /// The status of the filter. 
        /// </param>
        public FilterStatusMessage(FilterStatus status)
        {
            Status = status;
            CooldownDuration = TimeSpan.Zero;
        }

        /// <summary>
        /// Constructs a new FilterStatusMessage instance with the Status set to Cooldown. 
        /// </summary>
        /// <param name="cooldownDuration">
        /// The duration of the cooldown to be enforced.
        /// </param>
        public FilterStatusMessage(TimeSpan cooldownDuration)
        {
            Status = FilterStatus.CooldownPeriodEnforced;
            CooldownDuration = cooldownDuration;
        }
    }
}