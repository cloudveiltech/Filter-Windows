//
//  main.m
//  mac-native-tester
//
//  Created by Kent Friesen on 10/18/18.
//  Copyright © 2018 CloudVeil Technology, Inc. All rights reserved.
//

#import <Foundation/Foundation.h>
#import <NetworkServices.h>
#import <Filter_Platform_Mac_Native.h>

void nativeLog(int severity, const char* str) {
    printf("%d: %s\n", severity, str);
}

int main(int argc, const char * argv[]) {
    @autoreleasepool {
        // insert code here...
        NSLog(@"Hello, World!");
        
        bool quitting = false;
        char* buffer = nil;
        size_t bufferSize = 0;
        
        SetNativeLogCallback(nativeLog);
        
        EnforceDns("8.9.9.9", "8.4.4.8");
        SetProxy("127.0.0.1", 54000, 13301);
        exit(0);
        
        while(!quitting) {
            printf("> ");
            
            if(buffer != nil) {
                free(buffer);
                buffer = nil;
            }
            
            bufferSize = 0;
            
            getline(&buffer, &bufferSize, stdin);
            
            NSString* string = [NSString stringWithUTF8String:buffer];
            NSArray* parts = [string componentsSeparatedByString:@" "];
            
            if([parts[0] isEqual: @"set-proxy"]) {
                NSString* hostname = parts[1];
                NSString* httpPortString = parts[2];
                NSString* httpsPortString = parts[3];
                
                const char* cHostname = ConvertCFStringToUTF8((__bridge CFStringRef)hostname);
                int httpPort = [httpPortString intValue];
                int httpsPort = [httpsPortString intValue];
                
                bool result = SetProxy(cHostname, httpPort, httpsPort);
                if(!result) {
                    NativeLog(NativeLogError, @"SetProxy failed.");
                } else {
                    NativeLog(NativeLogInfo, @"SetProxy succeeded.");
                }
            }
        }
    }
    return 0;
}
