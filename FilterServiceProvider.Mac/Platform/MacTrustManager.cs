// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using FilterProvider.Common.Platform;

namespace FilterServiceProvider.Mac.Platform
{
    public class MacTrustManager : IPlatformTrust
    {
        public MacTrustManager()
        {
        }

        public void EstablishTrust()
        {
            // No trust needs to be established at this point?
        }
    }
}
