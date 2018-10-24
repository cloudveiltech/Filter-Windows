/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

using CloudVeilGUI.Models;

namespace CloudVeilGUI.ViewModels
{
    public class SelfModerationViewModel : BaseViewModel
    {
        public ObservableCollection<SelfModerationEntry> SelfModerationEntries { get; set; }
        
        public SelfModerationViewModel()
        {
            Title = "Self-moderation";
            SelfModerationEntries = new ObservableCollection<SelfModerationEntry>();

            SelfModerationEntries.Add(new SelfModerationEntry { Url = "bbc.com", SelfModerationType = SelfModerationType.Blacklist });
            SelfModerationEntries.Add(new SelfModerationEntry { Url = "npr.org", SelfModerationType = SelfModerationType.Blacklist });
        }
    }
}
