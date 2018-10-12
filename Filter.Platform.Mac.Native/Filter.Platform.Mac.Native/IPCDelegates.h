// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//

#ifndef IPCDelegates_h
#define IPCDelegates_h

typedef void (*ConnectionDelegate)(void);
typedef bool (*IncomingMessageDelegate)(const unsigned char* data, UInt64 length);

#endif /* IPCDelegates_h */
