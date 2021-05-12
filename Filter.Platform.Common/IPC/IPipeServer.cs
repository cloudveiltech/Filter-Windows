/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using CloudVeil.IPC.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common.IPC
{
    public delegate void ConnectionHandler(IPipeServer server);
    public delegate void MessageHandler(IPipeServer server, BaseMessage message);
    public delegate void PipeExceptionHandler(Exception exception);

    public interface IPipeServer
    {
        event ConnectionHandler ClientConnected;
        event ConnectionHandler ClientDisconnected;
        event MessageHandler ClientMessage;
        event PipeExceptionHandler Error;

        void Start();
        void Stop();

        void PushMessage(BaseMessage msg);
    }
}
