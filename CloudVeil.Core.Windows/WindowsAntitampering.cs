/*
* Copyright � 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
﻿using Filter.Platform.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gui.CloudVeil.Util;

namespace CloudVeil.Core.Windows
{
    public class WindowsAntitampering : IAntitampering
    {
        public bool IsProcessProtected => CriticalKernelProcessUtility.IsMyProcessKernelCritical;

        public void DisableProcessProtection()
        {
            CriticalKernelProcessUtility.SetMyProcessAsNonKernelCritical();
        }

        public void EnableProcessProtection()
        {
            CriticalKernelProcessUtility.SetMyProcessAsKernelCritical();
        }
    }
}
