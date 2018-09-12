/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Text;

namespace CloudVeilGUI.Platform.Common
{
    public interface IFilterStarter
    {
        /// <summary>
        /// Checks to see if the filter service is running. If it is not, run the FilterAgent.{Platform} executable.
        /// </summary>
        void StartFilter();
    }
}
