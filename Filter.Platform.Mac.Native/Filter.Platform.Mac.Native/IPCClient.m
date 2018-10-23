// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

#import "IPCClient.h"

#import <XPC/XPC.h>

static void handleEvent(IpcClientHandle handle, xpc_object_t event);
static void handleIncomingMessage(IpcClientHandle handle, xpc_object_t event);

IpcClientHandle IpcClient_Create(const char* channel, ConnectionDelegate onConnected, ConnectionDelegate onDisconnected) {
    IpcClientHandle handle = CFAllocatorAllocate(nil, sizeof(IpcClient), 0);
    handle->onConnected = onConnected;
    handle->onDisconnected = onDisconnected;
    
    xpc_connection_t conn = xpc_connection_create(channel, NULL);
    
    handle->connection = conn;
    
    xpc_connection_set_event_handler(conn, ^(xpc_object_t  _Nonnull event) {
        handleEvent(handle, event);
    });
    
    return handle;
}

void IpcClient_Destroy(IpcClientHandle handle) {
    if(handle == nil) {
        return;
    }
    
    if(handle->connection != nil) {
        xpc_connection_cancel(handle->connection);
        handle->connection = nil;
    }
    
    CFAllocatorDeallocate(nil, handle);
}

void IpcClient_PushMessage(IpcClientHandle handle, unsigned char* data, int length, bool broadcast) {
    xpc_object_t obj = xpc_dictionary_create(nil, nil, 0);
    
    xpc_dictionary_set_bool(obj, "broadcast", broadcast);
    xpc_dictionary_set_data(obj, "msg", data, length);
    
    xpc_connection_send_message(handle->connection, obj);
}

void IpcClient_SetOnClientConnected(IpcClientHandle handle, ConnectionDelegate delegate) {
    if(handle == nil) {
        return;
    }
    
    handle->onConnected = delegate;
}
void IpcClient_SetOnClientDisconnected(IpcClientHandle handle, ConnectionDelegate delegate) {
    if(handle == nil) {
        return;
    }
    
    handle->onDisconnected = delegate;
}

void IpcClient_SetOnIncomingMessage(IpcClientHandle handle, IncomingMessageDelegate delegate) {
    if(handle == nil) {
        return;
    }
    
    handle->onIncomingMessage = delegate;
}

// Our low-level XPC Dictionary protocol is this:
// ["broadcast"] BOOL - This broadcasts the XPC message to all other connected clients. For client-to-client communication.
// ["msg"] DATA/BYTE[] - This is a string of bytes encoded from the higher-level pipe server implementation.

static void handleEvent(IpcClientHandle handle, xpc_object_t event) {
    if(xpc_get_type(event) == XPC_TYPE_DICTIONARY) {
        if(xpc_dictionary_get_bool(event, "connection")) {
            if(handle->onConnected) {
                handle->onConnected();
            }
        } else {
            handleIncomingMessage(handle, event);
        }
    } else if(event == XPC_ERROR_CONNECTION_INVALID) {
        if(handle->onDisconnected) {
            handle->onDisconnected();
        }
    }
}

static void handleIncomingMessage(IpcClientHandle handle, xpc_object_t event) {
    size_t len = 0;
    const void* data = xpc_dictionary_get_data(event, "msg", &len);
    
    if(handle->onIncomingMessage) {
        handle->onIncomingMessage(data, (int)len);
    }
}
