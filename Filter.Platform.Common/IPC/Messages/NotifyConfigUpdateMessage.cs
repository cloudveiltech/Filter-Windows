/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Filter.Platform.Common.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeil.IPC.Messages
{
    [Serializable]
    public class NotifyConfigUpdateMessage : BaseMessage
    {
        public ConfigUpdateResult UpdateResult { get; set; }

        public NotifyConfigUpdateMessage(ConfigUpdateResult result)
        {
            UpdateResult = result;
        }
    }
}
