/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;

namespace Citadel.IPC.Messages
{
    /// <summary>
    /// The ServiceUpdateNotificationMessage class represents an IPC communication between server
    /// (Service) and client (GUI) about application updates. The client receiving this message means
    /// that the client should shut down and schedule a restart after a long delay to allow an update
    /// to complete.
    /// </summary>
    [Serializable]
    public class ServerUpdateNotificationMessage : ServerOnlyMessage
    {
        public ServerUpdateNotificationMessage()
        {
        }
    }
}