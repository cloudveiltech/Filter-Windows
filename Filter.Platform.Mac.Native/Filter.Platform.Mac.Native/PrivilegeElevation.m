//
//  PrivilegeElevation.m
//  Filter.Platform.Mac.Native
//
//  Created by Kent Friesen on 10/10/18.
//  Copyright © 2018 CloudVeil Technology, Inc. All rights reserved.
//

#import "PrivilegeElevation.h"
#import <Foundation/Foundation.h>

bool BlessHelper(const char* label, NSError** errorPtr) {
    bool result = false;
    NSError* error = nil;
    
    AuthorizationRef authRef = nil;
    OSStatus status = AuthorizationCreate(NULL, kAuthorizationEmptyEnvironment, kAuthorizationFlagDefaults, &authRef);
    
    if(status != errAuthorizationSuccess) {
        return false;
    }
    
    AuthorizationItem authItem = { kSMRightBlessPrivilegedHelper, 0, NULL, 0 };
    AuthorizationRights authRights = { 1, &authItem };
    AuthorizationFlags flags = kAuthorizationFlagDefaults |
                                kAuthorizationFlagInteractionAllowed |
                                kAuthorizationFlagPreAuthorize |
                                kAuthorizationFlagExtendRights;
    
    status = AuthorizationCopyRights(authRef, &authRights, kAuthorizationEmptyEnvironment, flags, NULL);
    
    if(status != errAuthorizationSuccess) {
        error = [NSError errorWithDomain:NSOSStatusErrorDomain code:status userInfo:nil];
    } else {
        CFErrorRef cfError;
        CFStringRef cfLabel = CFStringCreateWithCString(kCFAllocatorDefault, label, kCFStringEncodingUTF8);
        
        result = (bool) SMJobBless(kSMDomainSystemLaunchd, cfLabel, authRef, &cfError);
        
        if(cfLabel != nil) {
            CFRelease(cfLabel);
            cfLabel = nil;
        }
        
        if(!result) {
            error = CFBridgingRelease(cfError);
        }
    }
    
    if(!result && (errorPtr != nil)) {
        if(error != nil) {
            *errorPtr = error;
        }
    }
    
    return result;
}
