using System;

namespace CloudVeil.IPC.Messages
{
    [Serializable]
    public enum IpcMessageBehavior
    {
        /// <summary>
        /// We can request an action
        /// </summary>
        Action,

        /// <summary>
        /// We can send a notification
        /// </summary>
        Notification,

        /// <summary>
        /// We can request or send data
        /// </summary>
        Data
    }
}