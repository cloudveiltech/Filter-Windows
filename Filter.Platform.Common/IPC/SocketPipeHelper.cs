// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
namespace Filter.Platform.Common.IPC
{
    public enum MessageType
    {
        ConnectionAccepted,
        DisconnectionNotification,
        Message,
        BroadcastMessage,

        Invalid = 0xff
    }

    public static class SocketPipeHelper
    {
        internal const byte MagicByte = 0xC0;

        public static MessageType GetMessageType(byte[] buffer)
        {
            return (MessageType)buffer[2];
        }

        public static byte[] BuildMessage(MessageType messageType, byte[] messageBuffer)
        {
            int length = messageBuffer == null ? 0 : messageBuffer.Length;

            byte[] msg = new byte[8 + length];
            msg[0] = MagicByte;
            msg[1] = 0;
            msg[2] = (byte)messageType;
            msg[3] = 0;

            byte[] lenBytes = BitConverter.GetBytes(length);
            Buffer.BlockCopy(lenBytes, 0, msg, 4, 4);

            if (messageBuffer != null)
            {
                Buffer.BlockCopy(messageBuffer, 0, msg, 8, length);
            }

            return msg;
        }
    }
}
