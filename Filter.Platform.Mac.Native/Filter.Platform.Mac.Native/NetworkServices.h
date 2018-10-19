// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

#ifndef DnsEnforcement_h
#define DnsEnforcement_h

#import <Foundation/Foundation.h>
#import <SystemConfiguration/SystemConfiguration.h>

typedef bool (*NetworkServiceFn)(SCNetworkServiceRef networkService, void* cbData);

bool touchDynamicStore(void);

bool EnforceDns(const char* primary, const char* secondary);

bool SetProxy(const char* hostname, int httpPort, int httpsPort);

// TODO: Create a CheckProxy() implementation.

// Uses SCNetworkReachability APIs to determine whether we have internet access.
bool IsInternetReachable(const char* hostname);

bool RemoveFromKeychain(SecCertificateRef certificate);

SecCertificateRef GetFromKeychain(const char* label);

SecCertificateRef AddToKeychain(void* data, int length, const char* label);

void EnsureCertificateTrust(SecCertificateRef cert);

void* GetCertificateBytes(SecCertificateRef cert, int* length);

// The next two lines are extremely evil. They are here to help us manage native memory from C# code.
// This is an extremely bad code-smell.
// This is done because we return a non-GC allocated buffer from GetCertificateBytes() and need a way to release that buffer once we're done marshalling to a C# type.

// A hack to allow us to call CFRelease() for our certificates.
void __CFRelease(CFTypeRef ptr);

// A hack to allow us to call CFAllocatorDeallocate() for our buffer.
void __CFDeallocate(void* ptr);

#endif /* DnsEnforcement_h */
