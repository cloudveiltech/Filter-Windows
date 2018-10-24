/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.Models
{
    /// <summary>
    /// This is a dummy class for now. Someday, we'll move this to Citadel.Core.Windows so that we can use it in both the 
    /// GUI and the service.
    /// </summary>
    public class SelfModerationEntry
    {
        public string Url { get; set; }

        // We don't really need a self-moderation type for block only, but I think it would be wise to use the same 
        // system for per-user whitelists.
        public SelfModerationType SelfModerationType { get; set; }
    }
}
