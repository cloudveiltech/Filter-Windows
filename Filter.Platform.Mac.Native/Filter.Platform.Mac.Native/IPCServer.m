// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

#import "IPCServer.h"

#import <XPC/XPC.h>
#import <pthread.h>

// Our low-level XPC Dictionary protocol is this:
// ["broadcast"] BOOL - This broadcasts the XPC message to all other connected clients. For client-to-client communication.
// ["msg"] DATA/BYTE[] - This is a string of bytes encoded from the higher-level pipe server implementation.

IPCServerHandle globalServerHandle = nil;

static void peer_event_handler(xpc_connection_t peer, xpc_object_t event);

// Low level protocol functions.
static void handleIncomingMessage(IPCServerHandle handle, xpc_connection_t sender, xpc_object_t event);
static void handleClientConnect(IPCServerHandle handle, xpc_connection_t sender);
static void handleClientDisconnect(IPCServerHandle handle, xpc_connection_t disconnected);
static void broadcastMessage(IPCServerHandle handle, xpc_object_t message, xpc_connection_t sender);

static void handleClientConnect(IPCServerHandle handle, xpc_connection_t sender) {
    if(handle == nil) {
        return;
    }
    
    if(handle->connectedClients == nil) {
        handle->connectedClients = CFArrayCreateMutable(nil, 4, nil);
    }
    
    CFArrayAppendValue(handle->connectedClients, (__bridge_retained void*)sender);
    
    // Now, send back a connection ack so our pipe client knows we're listening to the connection.
    xpc_object_t connectionObj = xpc_dictionary_create(nil, nil, 0);
    xpc_dictionary_set_bool(connectionObj, "connection", true);
    
    xpc_connection_send_message(sender, connectionObj);
    
    if(handle->onClientConnected) {
        handle->onClientConnected();
    }
}

static void handleClientDisconnect(IPCServerHandle handle, xpc_connection_t disconnected) {
    if(handle == nil) {
        return;
    }
    
    if(handle->connectedClients == nil) {
        return;
    }
    
    CFIndex i = 0, arrayCount = CFArrayGetCount(handle->connectedClients);
    for(i = 0; i < arrayCount; i++) {
        const void* _itr = CFArrayGetValueAtIndex(handle->connectedClients, i);
        const xpc_connection_t conn = (__bridge_transfer xpc_connection_t)_itr;
        
        if(xpc_equal(disconnected, conn)) {
            CFArrayRemoveValueAtIndex(handle->connectedClients, i);
        }
    }
}

static void handleIncomingMessage(IPCServerHandle handle, xpc_connection_t sender, xpc_object_t event) {
    bool isBroadcast = xpc_dictionary_get_bool(event, "broadcast");
    
    if(isBroadcast) {
        broadcastMessage(handle, event, sender);
    } else {
        size_t length = 0;
        const unsigned char* data = nil;
        
        data = (unsigned char*)xpc_dictionary_get_data(event, "msg", &length);
        
        if(handle->onIncomingMessage) {
            handle->onIncomingMessage(data, length);
        }
    }
}

// broadcastMessage: Broadcasts message to all clients stored by server handle.
//  sender: optional
static void broadcastMessage(IPCServerHandle handle, xpc_object_t message, xpc_connection_t sender) {
    if(handle == nil) {
        return;
    }
    
    CFMutableArrayRef clients = handle->connectedClients;
    
    CFIndex arrayCount = CFArrayGetCount(clients), i = 0;
    for(i = 0; i < arrayCount; i++) {
        xpc_connection_t conn = (__bridge xpc_connection_t)CFArrayGetValueAtIndex(clients, i);
        
        if(sender != nil && !xpc_equal(conn, sender)) {
            continue; // Skip sending message to the sender, if set.
        }
        
        xpc_connection_send_message(conn, message);
    }
}

static void connection_handler(xpc_connection_t peer) {
    pthread_testcancel();
    
    xpc_connection_set_event_handler(peer, ^(xpc_object_t  _Nonnull event) {
        peer_event_handler(peer, event);
    });
    
    xpc_connection_resume(peer);
    
    handleClientConnect(globalServerHandle, peer);
}

static void peer_event_handler(xpc_connection_t peer, xpc_object_t event) {
    pthread_testcancel();
    
    // TODO?
    // How to handle peer disconnect?
    if(event == XPC_ERROR_CONNECTION_INVALID) {
        handleClientDisconnect(globalServerHandle, peer);
    } else {
        xpc_type_t xpcType = xpc_get_type(event);
        
        if(xpcType == XPC_TYPE_DICTIONARY) {
            handleIncomingMessage(globalServerHandle, peer, event);
        }
    }
}

void* IPCServerThread(void* data) {
    xpc_main(connection_handler);
    
    return NULL;
}

IPCServerHandle GetGlobalIPCServer() {
    if(globalServerHandle != nil) {
        return globalServerHandle;
    }
    
    // New IPC server starts a new thread so we can call xpc_main on it.
    IPCServerHandle server = CFAllocatorAllocate(NULL, sizeof(IpcServer), 0);
    
    if(server == nil) {
        return nil;
    }
    
    server->stopping = false;
    
    pthread_t* thread = CFAllocatorAllocate(NULL, sizeof(pthread_t), 0);
    
    if(thread == nil) {
        CFAllocatorDeallocate(NULL, server);
        return nil;
    }
    
    if(pthread_create(thread, NULL, IPCServerThread, server)) {
        CFAllocatorDeallocate(NULL, server);
        CFAllocatorDeallocate(NULL, thread);
        return nil;
    }
    
    server->connectedClients = CFArrayCreateMutable(nil, 4, nil);
    
    globalServerHandle = server;
    
    // IPCServerThread takes care of everything from here on out.
    return server;
}

void IPCServer_SetOnClientConnected(IPCServerHandle handle, ConnectionDelegate delegate) {
    if(handle == nil) {
        return;
    }
    
    handle->onClientConnected = delegate;
}

void IPCServer_SetOnClientDisconnected(IPCServerHandle handle, ConnectionDelegate delegate) {
    if(handle == nil) {
        return;
    }
    
    handle->onClientDisconnected = delegate;
}

void IPCServer_SetOnIncomingMessage(IPCServerHandle handle, IncomingMessageDelegate delegate) {
    if(handle == nil) {
        return;
    }
    
    handle->onIncomingMessage = delegate;
}

// No need for an isBroadcast boolean here, because we send to all clients.
bool IPCServer_PushMessage(IPCServerHandle handle, unsigned char* data, int length) {
    xpc_object_t newMsg = xpc_dictionary_create(nil, nil, 0);
    xpc_dictionary_set_bool(newMsg, "broadcast", false);
    xpc_dictionary_set_data(newMsg, "msg", data, length);
    
    broadcastMessage(handle, newMsg, nil);
    
    return true;
}

// TODO: This looks like nasty bug causing code.
void StopIPCServer(IPCServerHandle handle) {
    handle->stopping = true;
    
    CFIndex i, arrayCount = CFArrayGetCount(handle->connectedClients);
    for(i = 0; i < arrayCount; i++) {
        xpc_connection_t conn = (__bridge xpc_connection_t)CFArrayGetValueAtIndex(handle->connectedClients, i);
        xpc_connection_cancel(conn);
    }
    
    CFRelease(handle->connectedClients);
    handle->connectedClients = nil;
    
    pthread_cancel(*handle->runningThread);
    
    CFAllocatorDeallocate(nil, handle->runningThread);
}
