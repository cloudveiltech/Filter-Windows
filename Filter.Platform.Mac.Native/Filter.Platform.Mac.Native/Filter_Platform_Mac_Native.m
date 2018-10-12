// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

#import "Filter_Platform_Mac_Native.h"


const char* ConvertCFStringToUTF8(CFStringRef cfStr);

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
