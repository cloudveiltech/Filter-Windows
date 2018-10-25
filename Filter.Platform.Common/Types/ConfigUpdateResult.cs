/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Filter.Platform.Common.Types
{
    /// <summary>
    /// Now used instead of a boolean to give callers of UpdateAndWriteList() a better idea of what's going on.
    /// </summary>
    [Serializable]
    public enum ConfigUpdateResult
    {
        /// <summary>
        /// A new configuration file was detected, downloaded, and saved.
        /// </summary>
        Updated = 1,

        /// <summary>
        /// No configuration file was downloaded because the current config was detected as up to date.
        /// </summary>
        UpToDate = 2,

        /// <summary>
        /// Returned when there is no internet and the configuration file needs updating.
        /// 
        /// Not sure what makes the difference, but it looks like if I check for updates without internet first, I get this code back.
        /// If I check for updates with internet, and then unplug, I get UpToDate from UpdateAndWriteList
        /// </summary>
        NoInternet = 3,

        /// <summary>
        /// Unspecified error occurred while attempting to update configuration.
        /// </summary>
        ErrorOccurred = 4,

        /// <summary>
        /// A flag used to indicate whether or not the app has an update available.
        /// NOTE: This does not take the place of the existing app update infrastructure. It is only in place for the sake of displaying the correct thing by the sync button.
        /// </summary>
        AppUpdateAvailable = 1 << 8
    }
}
