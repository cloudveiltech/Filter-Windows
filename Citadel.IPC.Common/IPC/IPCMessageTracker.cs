using Citadel.IPC.Messages;
using System;
using System.Collections.Generic;

namespace Citadel.IPC
{
    public class IPCMessageTracker
    {
        private class IPCMessageData
        {
            public BaseMessage Message { get; set; }
            public GenericReplyHandler Handler { get; set; }
        }

        private List<IPCMessageData> m_messageList;
        private object m_lock;

        public IPCMessageTracker()
        {
            m_messageList = new List<IPCMessageData>();
            m_lock = new object();
        }

        public void AddMessage(BaseMessage message, GenericReplyHandler handler)
        {
            lock (m_lock)
            {
                m_messageList.Add(new IPCMessageData() { Message = message, Handler = handler });
            }
        }

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

                        if (data.Message.Id == message.ReplyToId)
                        {
                            m_messageList.Remove(data);

                            if (data.Handler != null)
                            {
                                bool ret = data.Handler(message);
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
            catch (Exception e)
            {
                return false;
            }
        }
    }
}