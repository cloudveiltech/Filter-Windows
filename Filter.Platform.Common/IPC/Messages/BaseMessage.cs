/*
* Copyright © 2017-2018 Cloudveil Technology Inc.  
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
    public class IpcMessage : BaseMessage
    {
        /// <summary>
        /// Functionally equivalent to the request path in a REST API.
        /// Think the "/endpoint" part in "GET /endpoint?param0=p
        /// </summary>
        public IpcCall Call { get; set; }

        /// <summary>
        /// Functionally equivalent to HTTP method in a REST API.
        /// Think the "GET" part in "GET /endpoint?param0=p
        /// </summary>
        public IpcMessageMethod Method { get; set; }

        /// <summary>
        /// The data object associated with the <see cref="IpcCall"/>
        /// Can be null.
        /// </summary>
        public object Data { get; set; }

        public static IpcMessage Send(IpcCall call, object data)
        {
            return new IpcMessage()
            {
                Method = IpcMessageMethod.Send,
                Call = call,
                Data = data
            };
        }

        public static IpcMessage Request(IpcCall call, object data = null)
        {
            return new IpcMessage()
            {
                Method = IpcMessageMethod.Request,
                Call = call
            };
        }
    }

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
