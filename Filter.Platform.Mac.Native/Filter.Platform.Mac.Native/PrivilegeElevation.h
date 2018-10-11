//
//  PrivilegeElevation.h
//  Filter.Platform.Mac.Native
//
//  Created by Kent Friesen on 10/10/18.
//  Copyright © 2018 CloudVeil Technology, Inc. All rights reserved.
//

#ifndef PrivilegeElevation_h
#define PrivilegeElevation_h

#import <Foundation/Foundation.h>
#import <ServiceManagement/ServiceManagement.h>
#import <Security/Authorization.h>

bool BlessHelper(const char* label, NSError** error);

#endif /* PrivilegeElevation_h */
