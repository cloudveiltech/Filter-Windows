/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common
{
    public interface IAntitampering
    {
        bool IsProcessProtected { get; }

        /// <summary>
        /// If a platform has any way to protect our process, implement it here.
        /// </summary>
        void EnableProcessProtection();

        /// <summary>
        /// Should disable our platform-specific kernel process protections.
        /// </summary>
        void DisableProcessProtection();
    }
}
