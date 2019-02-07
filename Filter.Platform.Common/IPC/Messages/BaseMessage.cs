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
    [Serializable]
    public class IpcMessage<T> : IpcMessage
    {
        public T Data
        {
            get => (T)DataObject;
            set => DataObject = value;
        }
    }

    [Serializable]
    public class IpcMessage : BaseMessage
    {
        public IpcMessage<T> As<T>()
        {
            return new IpcMessage<T>()
            {
                Call = this.Call,
                Method = this.Method,
                DataObject = this.DataObject,
                Id = this.Id,
                ReplyToId = this.ReplyToId
            };
        }

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
        public object DataObject { get; set; }

        public static IpcMessage Send(IpcCall call, object data)
        {
            return new IpcMessage()
            {
                Method = IpcMessageMethod.Send,
                Call = call,
                DataObject = data
            };
        }

        public static IpcMessage Request(IpcCall call, object data = null)
        {
            return new IpcMessage()
            {
                Method = IpcMessageMethod.Request,
                Call = call,
                DataObject = data
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

        public ReplyHandlerClass SendReply<T>(IpcCommunicator comm, IpcCall call, T data)
        {
            return comm.Send<T>(call, data, this);
        }

        public ReplyHandlerClass<TResponse> SendReply<T, TResponse>(IpcCommunicator comm, IpcCall call, T data)
        {
            return comm.Send<T, TResponse>(call, data, this);
        }
    }
}
