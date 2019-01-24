/*
* Copyright © 2019 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace Citadel.IPC.Messages
{
    [Serializable]
    public class AddSelfModerationEntryMessage : BaseMessage
    {
        public AddSelfModerationEntryMessage(string site)
        {
            Site = site;
        }
    
        public string Site { get; set; }
    }
}
