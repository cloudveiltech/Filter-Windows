/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using CloudVeilGUI.Models;
using NodaTime.TimeZones;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CloudVeilGUI.ViewModels
{
    public class TimeRestrictionsViewModel
    {
        public bool IsTimeRestrictionsEnabled { get; set; }
        
        public TimeSpan FromTime { get; set; }
        public TimeSpan ToTime { get; set; }

        public TimeRestrictionsViewModel()
        {

        }
    }
}
