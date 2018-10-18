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

SecCertificateRef GetFromKeychain(const char* label);

SecCertificateRef AddToKeychain(void* data, int length, const char* label);

void EnsureCertificateTrust(SecCertificateRef cert);

void* GetCertificateBytes(SecCertificateRef cert, int* length);

// A hack to allow us to call CFRelease() for our certificates and buffers
void __CFRelease(CFTypeRef ptr);

#endif /* DnsEnforcement_h */
