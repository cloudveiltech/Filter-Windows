// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

#import <Foundation/Foundation.h>
#import "IPCDelegates.h"

// C delegates - C# has its own P/Invoke capable delegates defined elsewhere.

typedef struct IpcServer {
    ConnectionDelegate onClientConnected;
    ConnectionDelegate onClientDisconnected;
    
    IncomingMessageDelegate onIncomingMessage;
    
    // CFArray of xpc_connection_t
    CFMutableArrayRef connectedClients;
    
    pthread_t* runningThread;
    
    // This gets set to true when stopping the IPC server.
    bool stopping;
} IpcServer;

typedef IpcServer* IPCServerHandle;

IPCServerHandle GetGlobalIPCServer(void);

void IPCServer_SetOnClientConnected(IPCServerHandle handle, ConnectionDelegate delegate);
void IPCServer_SetOnClientDisconnected(IPCServerHandle handle, ConnectionDelegate delegate);

void IPCServer_SetOnIncomingMessage(IPCServerHandle handle, IncomingMessageDelegate delegate);

// No need for an isBroadcast boolean here, because we send to all clients.
bool IPCServer_PushMessage(IPCServerHandle handle, unsigned char* data, int length);

void StopIPCServer(IPCServerHandle handle);
