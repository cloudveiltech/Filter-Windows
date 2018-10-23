// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

#import <Foundation/Foundation.h>

const char* GetSystemFingerprint(void);

const char* ConvertCFStringToUTF8(CFStringRef cfStr);

// This is to be used by the native code.
void NativeLog(int severity, NSString* str);

enum NativeLogSeverities {
    NativeLogTrace,
    NativeLogDebug,
    NativeLogInfo,
    NativeLogWarn,
    NativeLogError,
    NativeLogCritical
};

typedef void (*NativeLogCallback)(int severity, const char* str);

// Use this to hook a callback into the native code for so that native code can log to the same source as our C# code.
void SetNativeLogCallback(NativeLogCallback cb);

bool AcquireFileLock(const char* filename, int *pfd);

void ReleaseFileLock(int fd);

bool IsFileLocked(const char* filename);
