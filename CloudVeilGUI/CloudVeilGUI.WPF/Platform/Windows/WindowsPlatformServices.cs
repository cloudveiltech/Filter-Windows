/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Citadel.IPC;
using CloudVeilGUI.Platform.Common;
using Filter.Platform.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeilGUI.Platform.Windows
{
    public class WindowsPlatformServices : PlatformServices
    {
        public override IFilterStarter CreateFilterStarter()
        {
            return new WindowsFilterStarter();
        }
    }
}
