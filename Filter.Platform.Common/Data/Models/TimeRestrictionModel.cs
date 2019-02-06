/*
* Copyright © 2019 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using System;

namespace Filter.Platform.Common.Data.Models
{
    [Serializable]
    public class TimeRestrictionModel
    {
        public decimal[] EnabledThrough { get; set; }

        public bool RestrictionsEnabled { get; set; }
    }
}