/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.Models
{
    public enum MenuItemType
    {
        BlockedPages,
        SelfModeration,
        TimeRestrictions,
        RelaxedPolicy,
        Advanced,
        Support,
        Diagnostics
    }

    public class HomeMenuItem
    {
        public MenuItemType Id { get; set; }

        public string Title { get; set; }
    }
}
