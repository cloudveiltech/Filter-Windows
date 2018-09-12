/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.IPC.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common.IPC
{
    public delegate void ClientConnectionHandler();
    public delegate void ServerMessageHandler(BaseMessage msg);

    public interface IPipeClient
    {
        event ClientConnectionHandler Connected;
        event ClientConnectionHandler Disconnected;

        event ServerMessageHandler ServerMessage;
        event PipeExceptionHandler Error;

        bool AutoReconnect { get; set; }

        void Start();
        void Stop();

        void WaitForConnection();

        void PushMessage(BaseMessage msg);
    }
}
