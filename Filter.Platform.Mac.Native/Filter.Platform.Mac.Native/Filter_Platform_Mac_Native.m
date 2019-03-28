// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

#import "Filter_Platform_Mac_Native.h"

const char* GetSystemFingerprint() {
    CFStringRef serialNumber = NULL;
    
    io_service_t platformExpert = IOServiceGetMatchingService(kIOMasterPortDefault, IOServiceMatching("IOPlatformExpertDevice"));
    
    if(platformExpert) {
        CFTypeRef serialNumberAsCFString =
        IORegistryEntryCreateCFProperty(platformExpert,
                                        CFSTR(kIOPlatformSerialNumberKey),
                                        kCFAllocatorDefault, 0);
        
        if(serialNumberAsCFString) {
            serialNumber = serialNumberAsCFString;
        }
        
        IOObjectRelease(platformExpert);
    }
    
    if(serialNumber == nil) {
        return nil;
    }
    
    const char* ret = ConvertCFStringToUTF8(serialNumber);
    CFRelease(serialNumber);
    
    return ret;
}

const char* ConvertCFStringToUTF8(CFStringRef cfStr) {
    if(cfStr == nil) {
        return nil;
    }
    
    CFIndex length = CFStringGetLength(cfStr);
    CFIndex maxSize = CFStringGetMaximumSizeForEncoding(length, kCFStringEncodingUTF8);
    char* buffer = (char*)malloc(maxSize);
    if(CFStringGetCString(cfStr, buffer, maxSize, kCFStringEncodingUTF8)) {
        return buffer;
    }
    
    free(buffer);
    return nil;
}

static NativeLogCallback loggerCallback = nil;

void NativeLog(int severity, NSString* str) {
    const char* c_str = nil;
    
    if(loggerCallback) {
        c_str = ConvertCFStringToUTF8((__bridge CFStringRef)str);
        
        loggerCallback(severity, c_str);
    }
}

void SetNativeLogCallback(NativeLogCallback cb) {
    loggerCallback = cb;
}

bool AcquireFileLock(const char* filename, int* pfd) {
    int fd = open(filename, O_RDWR | O_CREAT);
    
    if(fd < 0) {
        NSLog(@"Was unable to create .cloudveil.lock because of %d", errno);
        return false;
    }
    
    struct flock fl;
    memset(&fl, 0, sizeof(struct flock));
    
    fcntl(fd, F_GETLK, &fl);
    
    if(fl.l_pid > 0) {
        NSLog(@".cloudveil.lock is currently locked by %d", fl.l_pid);
        return false;
    }
    
    fl.l_type = F_RDLCK;
    fl.l_whence = SEEK_SET;
    fl.l_start = 0;
    fl.l_len = 0;
    
    if(fcntl(fd, F_SETLK, &fl) == -1) {
        NSLog(@"Errno = %d", errno);
        return false;
    }
    
    if(pfd != nil) {
        *pfd = fd;
    }
    
    return true;
}

void ReleaseFileLock(int fd) {
    close(fd);
}

bool IsFileLocked(const char* filename) {
    int fd = open(filename, O_RDWR | O_CREAT);
    
    if(fd < 0) {
        switch(errno) {
            case EACCES:
            case EFAULT:
            case ENOMEM:
                return false;
                
            default:
                return true;
        }
    }
    
    struct flock fl;
    memset(&fl, 0, sizeof(struct flock));
    
    fcntl(fd, F_GETLK, &fl);
    
    if(fl.l_pid > 0) {
        return true;
    }
    
    return false;
}
