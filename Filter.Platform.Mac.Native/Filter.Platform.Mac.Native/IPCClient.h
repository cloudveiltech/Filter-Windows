// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

#import <Foundation/Foundation.h>
#import <XPC/XPC.h>
#import "IPCDelegates.h"

typedef struct IpcClient {
    xpc_connection_t connection;
    
    ConnectionDelegate onConnected;
    ConnectionDelegate onDisconnected;
    IncomingMessageDelegate onIncomingMessage;
} IpcClient;

typedef IpcClient* IpcClientHandle;

IpcClientHandle IpcClient_Create(const char* channel, ConnectionDelegate onConnected, ConnectionDelegate onDisconnected);

void IpcClient_Destroy(IpcClientHandle handle);

// IpcClient_PushMessage:
//   data: a byte array of data to send to the server, serialized from C# BinaryFormatter()
//   length: length of the data
//   broadcast: Whether the server should broadcast this message back to the other clients.
void IpcClient_PushMessage(IpcClientHandle handle, unsigned char* data, int length, bool broadcast);

void IpcClient_SetOnClientConnected(IpcClientHandle handle, ConnectionDelegate delegate);
void IpcClient_SetOnClientDisconnected(IpcClientHandle handle, ConnectionDelegate delegate);

void IpcClient_SetOnIncomingMessage(IpcClientHandle handle, IncomingMessageDelegate delegate);
