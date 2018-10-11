//
//  Filter_Platform_Mac_Native.m
//  Filter.Platform.Mac.Native
//
//  Created by Kent Friesen on 10/10/18.
//  Copyright © 2018 CloudVeil Technology, Inc. All rights reserved.
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
