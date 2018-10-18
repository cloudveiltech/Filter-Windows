// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

#import "IPCThread.h"

@implementation IPCThread

- (instancetype)initWithRunLoopSource: (CFRunLoopSourceRef)runLoopSource {
    self = [super init];
    m_runLoopSource = runLoopSource;
    
    return self;
}

- (void)main {
    CFRunLoopAddSource(CFRunLoopGetCurrent(), m_runLoopSource, kCFRunLoopDefaultMode);
    
    CFRunLoopRun();
}

- (void)stopThread {
    [self performSelector:@selector(_stop) onThread:self withObject:nil waitUntilDone:NO];
}

- (void)_stop {
    CFRunLoopStop(CFRunLoopGetCurrent());
}

@end
