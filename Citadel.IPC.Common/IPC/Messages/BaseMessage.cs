/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Citadel.IPC.Messages
{
    [Serializable]
    public class BaseMessage
    {
        /// <summary>
        /// This is an ID to identify a message.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// This is the ID to which this message is a reply.
        /// 
        /// If null, this is not a reply.
        /// </summary>
        public Guid ReplyToId { get; set; }

        public BaseMessage()
        {
            Id = Guid.NewGuid();
        }
    }
}
