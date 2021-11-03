/*
* Copyright � 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
﻿using CloudVeil.IPC.Messages;
using Filter.Platform.Common.Util;
using System;
using System.Collections.Generic;

namespace CloudVeil.IPC
{
    /// <summary>
    /// Used by IPCClient to track messages which are expecting replies.
    /// </summary>
    public class IPCMessageTracker
    {
        /// <summary>
        /// The internal wrapper for a message.
        /// 
        /// BaseMessage contains all the information needed to track the replies from the server (assuming the server has the correct info filled out)
        /// </summary>
        private class IPCMessageData
        {
            public BaseMessage Message { get; set; }
            public ReplyHandlerClass Handler { get; set; }

            public int Retries { get; set; } = 0;
        }

        private static NLog.Logger m_logger = LoggerUtil.GetAppWideLogger();
        private List<IPCMessageData> m_messageList;
        private object m_lock;

        private IpcCommunicator m_communicator;

        public IPCMessageTracker(IpcCommunicator communicator)
        {
            m_messageList = new List<IPCMessageData>();
            m_lock = new object();
            m_communicator = communicator;
        }

        /// <summary>
        /// Adds a message and a handler to the tracker to wait until HandleMessage is called with a reply.
        /// </summary>
        /// <param name="message">The message which was sent to the server</param>
        /// <param name="handler">The handler for the reply message.</param>
        public void AddMessage(BaseMessage message, ReplyHandlerClass handler, int retryNum)
        {
            lock (m_lock)
            {
                m_messageList.Add(new IPCMessageData() { Message = message, Handler = handler, Retries = retryNum });
            }
        }

        /// <summary>
        /// Intended to be called when a client disconnects from the IPC server for some reason.
        /// Makes sure that a server ends up handling a client's message.
        /// </summary>
        public void RetryMessages()
        {
            const int MaxRetries = 3;

            m_logger.Info("Retrying IPC messages.");
            lock (m_lock)
            {
                for(int i=0;i < m_messageList.Count; i++)
                {
                    var message = m_messageList[i];
                    if (message.Retries < MaxRetries)
                    {
                        m_communicator.PushMessage(message.Message, message.Handler, message.Retries + 1);
                    }
                }
            }
        }

        /// <summary>
        /// Called by IPCClient.OnServerMessage() before any of its own handlers handle the message.
        /// 
        /// If this function returns true, that means that this function had the capability of handling this message.
        /// </summary>
        /// <param name="message">The reply message from the IPC server</param>
        /// <returns>true if handled, false if not</returns>
        public bool HandleMessage(BaseMessage message)
        {
            try
            {
                lock (m_lock)
                {
                    for (int i = 0; i < m_messageList.Count; i++)
                    {
                        IPCMessageData data = null;
                        data = m_messageList[i];

                        if(data.Handler.DisposeIfDiscarded())
                        {
                            m_messageList.RemoveAt(i);
                            i--;
                            continue;
                        }

                        if (data.Message.Id == message.ReplyToId)
                        {
                            m_messageList.Remove(data);

                            if (data.Handler != null)
                            {
                                bool ret = data.Handler.TriggerHandler(message);
                                return ret;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }

                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}