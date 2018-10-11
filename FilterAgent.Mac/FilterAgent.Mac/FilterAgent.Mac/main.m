//
//  main.m
//  FilterAgent.Mac
//
//  Created by Kent Friesen on 10/10/18.
//  Copyright © 2018 CloudVeil Technology, Inc. All rights reserved.
//
// FilterAgent.Mac is a more important application to our MacOS filter than FilterAgent.Windows is to our Windows filter.
// 
#import <Foundation/Foundation.h>

int main(int argc, const char * argv[]) {
    if(argc <= 1) {
        fprintf(stderr, "You did not specify an argument for FilterAgent.Mac to run.\n");
        return 1;
    }
    
    if(!strcmp(argv[1], "install")) {
        installFilter();
    } else if(!strcmp(argv[1], "start")){
        startFilter();
    }
    
    @autoreleasepool {
        // insert code here...
        NSLog(@"Hello, World!");
    }
    return 0;
}
