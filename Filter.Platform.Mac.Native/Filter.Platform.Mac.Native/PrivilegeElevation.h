// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

#ifndef PrivilegeElevation_h
#define PrivilegeElevation_h

#import <Foundation/Foundation.h>
#import <ServiceManagement/ServiceManagement.h>
#import <Security/Authorization.h>

bool BlessHelper(const char* label, NSError** error);

bool IsEffectiveUserIdRoot();

#endif /* PrivilegeElevation_h */
