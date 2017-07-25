/*
* Copyright © 2017 Jesse Nicholson
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Diagnostics;

namespace Citadel.IPC.Messages
{
    /// <summary>
    /// The purpose of the ClientOnlyMessage class is to give a base that IPC messages designed
    /// exclusively for the client to create and inherit from. This class simply ensures that the
    /// executing process is not living in session 0 isolation space, where our server is expected to
    /// reside. If the executing process is in session 0 isolation, the default constructor of this
    /// class will throw an InvalidOperationException.
    /// </summary>
    [Serializable]
    public class ClientOnlyMessage : BaseMessage
    {
        /// <summary>
        /// Constructs a new ClientOnlyMessage instance. 
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// This class detects the session ID of the current executing process. This class is
        /// designed for exclusive use by the client side of an IPC channel. The server side has a
        /// hard requirement of being a service. Services, under Windows, are isolated in session 0.
        /// If the process executing this constructor is in session 0 isolation, this constructor
        /// will throw.
        /// </exception>
        public ClientOnlyMessage()
        {
            int procId = 0;

            try
            {
                procId = Process.GetCurrentProcess().SessionId;
            }
            catch { }

            if(procId == 0)
            {
                throw new InvalidOperationException("This IPC message type is designed exclusively for use by the client side of the IPC channel. The client side should never be in session 0 isolation. You are constructing this class from a session 0 process.");
            }
        }
    }
}