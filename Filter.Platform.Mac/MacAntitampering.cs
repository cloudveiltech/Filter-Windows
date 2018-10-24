﻿// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using Filter.Platform.Common;

namespace Filter.Platform.Mac
{
    public class MacAntitampering : IAntitampering
    {
        public MacAntitampering()
        {
        }

        public bool IsProcessProtected => false;

        public void DisableProcessProtection()
        {
            //throw new NotImplementedException();
        }

        public void EnableProcessProtection()
        {
            //throw new NotImplementedException();
        }
    }
}