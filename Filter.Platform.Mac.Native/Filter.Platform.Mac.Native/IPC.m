// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//


#import <Foundation/Foundation.h>
#include <mach/mach.h>
#include <mach/mach_time.h>

#import "IPC.h"
#import "Filter_Platform_Mac_Native.h"

CFDataRef IPC_BuildMessage(enum IPCMessageType messageType, const void* data, int length, bool broadcast);
CFDataRef IPCServer_ConnectionResponse();

static CFDataRef ServerCallback(CFMessagePortRef port, SInt32 messageId, CFDataRef data, void *info) {
    
    // Find IPCServerHandle that this port is attached to so that we can call its callback.
    IPCServerHandle handle = (IPCServerHandle)info;
    
    if(handle == nil || handle->MessageCallback == nil) {
        return nil;
    }
    
    // Convert CFDataRef into void* and int pair.
    CFIndex dataLength = CFDataGetLength(data);
    UInt8* buffer = malloc(dataLength);
    
    CFDataGetBytes(data, CFRangeMake(0, dataLength), buffer);
    
    // Now that we've got an IPC server handle, parse the message.
    IPCMessageHeader* header = (IPCMessageHeader*)buffer;
    
    if(header->magic != 'C') {
        return nil;
    }
    
    switch(header->messageType & ~BROADCAST_MESSAGE_FLAG) {
        case ipcClientConnection:
        {
            // Get client name and add it to our client list.
            if(handle->clients == nil) {
                handle->clients = CFArrayCreateMutable(nil, 16, nil);
            }
            
            CFStringRef cfName = CFStringCreateWithBytes(nil, buffer + sizeof(IPCMessageHeader), header->dataLength, kCFStringEncodingUTF8, false);
            
            CFArrayAppendValue(handle->clients, cfName);
            
            if(handle->ConnectionCallback != nil) {
                handle->ConnectionCallback();
            }
            
            return IPCServer_ConnectionResponse();
        }
            
        case ipcClientDisconnection:
        {
            if(handle->clients == nil) {
                break; // No need to remove the client name from our list.
            }
            
            CFStringRef cfName = CFStringCreateWithBytes(nil, buffer + sizeof(IPCMessageHeader), header->dataLength, kCFStringEncodingUTF8, false);
            
            CFIndex length = CFArrayGetCount(handle->clients);
            for(CFIndex i = 0; i < length; i++) {
                if(CFStringCompare(cfName, (CFStringRef)CFArrayGetValueAtIndex(handle->clients, i), 0)) {
                    CFRelease(CFArrayGetValueAtIndex(handle->clients, i));
                    
                    CFArrayRemoveValueAtIndex(handle->clients, i);
                    break;
                }
            }
            
            if(handle->DisconnectionCallback != nil) {
                handle->DisconnectionCallback();
            }
            
            return nil; // No need to send any response.
        }
            
        case ipcMessage:
        {
            if(header->messageType & BROADCAST_MESSAGE_FLAG) {
                IPCServer_SendToAll(handle, buffer + sizeof(IPCMessageHeader), header->dataLength);
            } else if(handle->MessageCallback != nil) {
                handle->MessageCallback(buffer + sizeof(IPCMessageHeader), header->dataLength);
            }
            
            // Mach ports makes it pretty easy to send back a response. TODO
            return nil;
        }
    }
    
    // No need to CFRelease(data), apple API does it for us.
    free(buffer);
    
    return nil;
}

CFDataRef IPCServer_ConnectionResponse() {
    return IPC_BuildMessage(ipcServerConnectionResponse, nil, 0, false);
}

CFDataRef IPC_BuildMessage(enum IPCMessageType messageType, const void* data, int length, bool broadcast) {
    if(data == nil) {
        length = 0;
    }
    
    UInt8* buffer = malloc(sizeof(IPCMessageHeader) + length);
    UInt8* dataBuffer = buffer + sizeof(IPCMessageHeader);
    
    IPCMessageHeader* header = (IPCMessageHeader*)buffer;
    
    header->magic = (UInt8)'C';
    header->dataLength = length;
    header->messageType = (UInt8)messageType;
    
    if(broadcast) {
        header->messageType |= BROADCAST_MESSAGE_FLAG;
    }
    
    header->padding[0] = 0;
    header->padding[1] = 0;
    
    if(data != nil) {
        memcpy(dataBuffer, data, length);
    }
    
    return CFDataCreate(nil, buffer, length + sizeof(IPCMessageHeader));
}

IPCServerHandle CreateIPCServer(const char* serverName, MessageCallbackDelegate callback, ConnectionCallbackDelegate onConnect, ConnectionCallbackDelegate onDisconnect) {
    
    IPCServerHandle ipcServer = malloc(sizeof(IPCServer));
    memset(ipcServer, 0, sizeof(IPCServer));
    
    ipcServer->MessageCallback = callback;
    ipcServer->ConnectionCallback = onConnect;
    ipcServer->DisconnectionCallback = onDisconnect;
    
    ipcServer->ServerName = CFStringCreateWithCString(nil, serverName, kCFStringEncodingUTF8);
    
    CFMessagePortContext* context = malloc(sizeof(CFMessagePortContext));
    memset(context, 0, sizeof(CFMessagePortContext));
    
    context->info = ipcServer;
    
    CFMessagePortRef serverPort = CFMessagePortCreateLocal(nil, ipcServer->ServerName, ServerCallback, context, nil);
    free(context);
    
    ipcServer->Port = serverPort;
    
    // FIXME: Will this work in a library called by C#? Will we need to create and manage our own runloop?
    CFRunLoopSourceRef runLoopSource = CFMessagePortCreateRunLoopSource(nil, serverPort, 0);
    ipcServer->runLoopSource = runLoopSource;
    
    //CFRunLoopAddSource(CFRunLoopGetCurrent(), runLoopSource, kCFRunLoopCommonModes);
    
    return ipcServer;
}

SInt32 IPC_SendInternal(SInt32 messageId, CFMessagePortRef localPort, CFStringRef remotePortName, CFDataRef cfData) {
    CFTimeInterval timeout = 1.0;
    
    CFMessagePortRef port = CFMessagePortCreateRemote(nil, remotePortName);
    
    SInt32 status = CFMessagePortSendRequest(port, messageId, cfData, timeout, timeout, nil, nil);
        
    return status;
}

void IPCServer_SendToAll(IPCServerHandle server, void* data, int dataLength) {
    if(server->clients == nil) {
        return;
    }
    
    CFDataRef message = IPC_BuildMessage(ipcMessage, data, dataLength, false);
    
    CFIndex length = CFArrayGetCount(server->clients);
    for(CFIndex i = 0; i < length; i++) {
        CFStringRef clientName = CFArrayGetValueAtIndex(server->clients, i);
        
        SInt32 status = IPC_SendInternal(server->nextMessageId++, server->Port, clientName, message);
        
        if(status != kCFMessagePortSuccess) {
            // TODO: Remove client port name from client list.
        }
    }
    
    CFRelease(message);
}

/**
 * Set the callback to call when the server receives a message.
 */
void IPCServer_SetCallback(IPCServerHandle handle, MessageCallbackDelegate callback) {
    handle->MessageCallback = callback;
}

/** IPCClient code */

/**
 Generates a random port name. */
UInt64 generateRandomNumber() {
    UInt64 randomId = 0;
    
    int status = SecRandomCopyBytes(kSecRandomDefault, sizeof(randomId), &randomId);
    
    if(status != errSecSuccess) {
        UInt64 absTime = mach_absolute_time();
        unsigned int seed0 = (unsigned int)(absTime ^ time(NULL));
        unsigned int seed1 = (unsigned int)(absTime + 100 ^ time(NULL));
        
        int rand0 = rand_r(&seed0);
        int rand1 = rand_r(&seed1);
        
        randomId = rand0 | ((UInt64) rand1 >> 32);
    }
    
    return randomId;
}

CFStringRef generateRandomPortName() {
    UInt64 randInt = generateRandomNumber();
    
    CFStringRef str = CFStringCreateWithFormat(nil, nil, CFSTR("port_%lu"), randInt);
    return str;
}

static CFDataRef ClientCallback(CFMessagePortRef port, SInt32 messageId, CFDataRef data, void *info);

IPCClientHandle CreateIPCClient(MessageCallbackDelegate callback, ConnectionCallbackDelegate onConnect, ConnectionCallbackDelegate onDisconnect) {
    CFMessagePortRef clientPort = nil;
    
    IPCClientHandle ipcClient = malloc(sizeof(IPCClient));
    memset(ipcClient, 0, sizeof(IPCClient));
    
    CFMessagePortContext* context = malloc(sizeof(CFMessagePortContext));
    context->info = ipcClient;
    context->release = nil;
    context->retain = nil;
    context->version = 0;
    context->copyDescription = nil;
    
    int maxRetries = 10;
    while(maxRetries > 0) {
        CFStringRef portName = generateRandomPortName();
        clientPort = CFMessagePortCreateLocal(nil, portName, ClientCallback, context, nil);
        
        if(clientPort != nil) {
            free(context);
            break;
        }
        
        maxRetries--;
    }
    
    if(maxRetries == 0) {
        free(ipcClient);
        return nil;
    }
    
    ipcClient->MessageCallback = callback;
    ipcClient->OnConnectedCallback = onConnect;
    ipcClient->Port = clientPort;
    
    CFRunLoopSourceRef runLoopSource = CFMessagePortCreateRunLoopSource(nil, clientPort, 0);
    ipcClient->runLoopSource = runLoopSource;
    
    CFRunLoopAddSource(CFRunLoopGetCurrent(), runLoopSource, kCFRunLoopCommonModes);
    
    return ipcClient;
}

void IPCClient_Connect(IPCClientHandle handle, const char* serverName) {
    CFDataRef connectionData = IPC_BuildMessage(ipcClientConnection, serverName, strlen(serverName), false);
    
    CFStringRef cfServerName = CFStringCreateWithCString(nil, serverName, kCFStringEncodingUTF8);
    
    handle->ServerName = cfServerName;
    
    handle->isWaitingConnect = 1;
    handle->isConnected = 0;
    
    IPC_SendInternal(handle->nextMessageId, handle->Port, cfServerName, connectionData);
}

void IPCClient_Disconnect(IPCClientHandle handle) {
    const char* clientName;
    
    clientName = ConvertCFStringToUTF8(CFMessagePortGetName(handle->Port));
    
    CFDataRef disconnectMe = IPC_BuildMessage(ipcClientDisconnection, clientName, strlen(clientName), false);
    
    free(clientName);
    
    IPC_SendInternal(handle->nextMessageId++, handle->Port, handle->ServerName, disconnectMe);
    
    CFRelease(handle->Port);
}

void IPCClient_Send(IPCClientHandle client, void* data, int dataLength, bool broadcast) {
    CFDataRef msg = IPC_BuildMessage(ipcMessage, data, dataLength, broadcast);
    
    IPC_SendInternal(client->nextMessageId++, client->Port, client->ServerName, msg);
}

void IPCClient_SetCallback(IPCClientHandle handle, MessageCallbackDelegate callback) {
    handle->MessageCallback = callback;
}

bool IPCClient_IsConnected(IPCClientHandle handle) {
    return handle->isConnected;
}

void IPCServer_Release(IPCServerHandle handle) {
    if(handle == nil) {
        return;
    }
    
    if(handle->clients != nil) {
        CFIndex length = CFArrayGetCount(handle->clients);
        CFIndex i = 0;
        for(i = 0; i < length; i++) {
            CFRelease(CFArrayGetValueAtIndex(handle->clients, i));
        }
        
        CFRelease(handle->clients);
    }
    
    if(handle->Port != nil) {
        CFRelease(handle->Port);
    }
    
    if(handle->ServerName != nil) {
        CFRelease(handle->ServerName);
    }
    
    free(handle);
}

void IPCClient_Release(IPCClientHandle handle) {
    if(handle == nil) {
        return;
    }
    
    if(handle->Port != nil) {
        CFRelease(handle->Port);
    }
    
    if(handle->ServerName != nil) {
        CFRelease(handle->ServerName);
    }
    
    free(handle);
}

static CFDataRef ClientCallback(CFMessagePortRef port, SInt32 messageId, CFDataRef data, void *info) {
    IPCClientHandle client = (IPCClientHandle)info;
    
    // Convert CFDataRef into void* and int pair.
    CFIndex dataLength = CFDataGetLength(data);
    UInt8* buffer = malloc(dataLength);
    
    CFDataGetBytes(data, CFRangeMake(0, dataLength), buffer);
    
    // Now that we've got an IPC server handle, parse the message.
    IPCMessageHeader* header = (IPCMessageHeader*)buffer;
    
    if(header->magic != 'C') {
        return nil;
    }
    
    switch(header->messageType) {
        case ipcServerConnectionResponse:
            client->isWaitingConnect = 0;
            client->isConnected = 1;
            
            if(client->OnConnectedCallback != nil) {
                client->OnConnectedCallback();
            }
            
            break;
            
        case ipcMessage:
            if(client->MessageCallback != nil) {
                client->MessageCallback(buffer + sizeof(IPCMessageHeader), header->dataLength);
            }
            
            break;
    }
    
    return nil;
}

IPCThread* IPCServer_StartLoop(IPCServerHandle server) {
    IPCThread* thread = [[IPCThread alloc] initWithRunLoopSource:server->runLoopSource];
    
    [thread start];
    
    return thread;
}

IPCThread* IPCClient_StartLoop(IPCClientHandle client) {
    IPCThread* thread = [[IPCThread alloc] initWithRunLoopSource:client->runLoopSource];
    
    [thread start];
    
    return thread;
}

void IPC_StopLoop(IPCThread* thread) {
    [thread stopThread];
    // FIXME: This thread leaks.
}
