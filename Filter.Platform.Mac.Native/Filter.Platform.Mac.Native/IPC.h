// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//


#ifndef IPC_h
#define IPC_h

#import <CoreFoundation/CoreFoundation.h>
#import "IPCThread.h"

/**
 MacOS implementation of our publish-subscribe server->client model using CoreFoundation/Mach ports.
 
 Some info:
 
 Bytes 0: ‘C’ magic marker
 Byte 1: message type
 1 - client connection
 2 - client disconnection
 3 - message
 4 - server connection response
 130 - broadcast message from client.
 bit 7 of byte 1 is the broadcast flag.
 Byte 2-3: padding
 Byte 4-7: data length (after header)
 
 Client connection and disconnection messages’ data are their chosen port name for the server to store.
 
 Client Connection protocol:
 
 1. Client generates a random name, and attempts to create a port with that name. If failed, repeat until found a name.
 2. Client connects to known server port and sends a client connection message with its name attached.
 3. Server sends back an ACK response.
 4. Client is now ready to begin sending messages.

 */

typedef void (*MessageCallbackDelegate)(void*, int);
typedef void (*ConnectionCallbackDelegate)(void);

#define BROADCAST_MESSAGE_FLAG 0x80

enum IPCMessageType {
    ipcClientConnection = 1,
    ipcClientDisconnection,
    ipcMessage,
    ipcServerConnectionResponse
};

typedef struct IPCServer {
    MessageCallbackDelegate MessageCallback;
    ConnectionCallbackDelegate ConnectionCallback;
    ConnectionCallbackDelegate DisconnectionCallback;
    
    CFStringRef ServerName;
    CFMessagePortRef Port;
    
    CFRunLoopSourceRef runLoopSource;
    
    SInt32 nextMessageId;
    
    // Since we're using the mach ports API, we don't have a concept of client->server.
    // This array will help us manage our abstraction layer to give C# an appearance of many clients to one server.
    CFMutableArrayRef clients;
} IPCServer;

typedef struct IPCMessageHeader {
    UInt8 magic; // should equal 'C'
    UInt8 messageType;
    UInt8 padding[2];
    UInt32 dataLength;
} IPCMessageHeader;

typedef IPCServer* IPCServerHandle;

IPCServerHandle CreateIPCServer(const char* serverName, MessageCallbackDelegate callback, ConnectionCallbackDelegate onConnect, ConnectionCallbackDelegate onDisconnect);

void IPCServer_SendToAll(IPCServerHandle server, void* data, int dataLength);

/**
 * Set the callback to call when the server receives a message.
 */
void IPCServer_SetCallback(IPCServerHandle handle, MessageCallbackDelegate callback);

typedef struct IPCClient {
    MessageCallbackDelegate MessageCallback;
    ConnectionCallbackDelegate OnConnectedCallback;
    
    CFStringRef ServerName;
    CFMessagePortRef Port;
    
    CFRunLoopSourceRef runLoopSource;
    
    SInt32 nextMessageId;
    
    SInt32 isWaitingConnect;
    SInt32 isConnected;
} IPCClient;

typedef IPCClient* IPCClientHandle;

IPCClientHandle CreateIPCClient(MessageCallbackDelegate callback, ConnectionCallbackDelegate onConnect, ConnectionCallbackDelegate onDisconnect);

void IPCClient_Connect(IPCClientHandle handle, const char* serverName);

void IPCClient_Disconnect(IPCClientHandle handle);

void IPCClient_Send(IPCClientHandle client, void* data, int dataLength, bool broadcast);

void IPCClient_SetCallback(IPCClientHandle handle, MessageCallbackDelegate callback);

bool IPCClient_IsConnected(IPCClientHandle handle);

void IPCServer_Release(IPCServerHandle handle);

void IPCClient_Release(IPCClientHandle handle);

IPCThread* IPCServer_StartLoop(IPCServerHandle server);

IPCThread* IPCClient_StartLoop(IPCClientHandle client);

void IPC_StopLoop(IPCThread* thread);

#endif /* IPC_h */
