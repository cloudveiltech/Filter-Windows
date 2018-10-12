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

bool EnforceDns(const char* primary, const char* secondary);

#endif /* DnsEnforcement_h */
