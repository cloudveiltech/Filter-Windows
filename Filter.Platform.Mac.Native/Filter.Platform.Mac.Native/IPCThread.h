// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//


#import <Foundation/Foundation.h>

@interface IPCThread : NSThread {
    CFRunLoopSourceRef m_runLoopSource;
}

- (instancetype)initWithRunLoopSource: (CFRunLoopSourceRef)runLoopSource;
- (void)stopThread;

@end
