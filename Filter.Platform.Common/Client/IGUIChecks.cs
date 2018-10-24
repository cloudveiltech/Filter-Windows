/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Filter.Platform.Common.Client
{
    public interface IGUIChecks
    {
        /// <summary>
        /// Determines whether the GUI process is in a session that it cannot display a window or receive user input.
        /// </summary>
        /// <returns>true if GUI in isolated session.</returns>
        bool IsInIsolatedSession();

        /// <summary>
        /// Make known through OS-specific means that the cloudveil GUI is running.
        /// </summary>
        /// <returns><c>true</c>, if lock was acquired, <c>false</c> otherwise.</returns>
        bool PublishRunningApp();

        /// <summary>
        /// Turn off OS-specific mutexes and locks that indicate that the GUI is running.
        /// </summary>
        /// <returns></returns>
        void UnpublishRunningApp();

        /// <summary>
        /// If there is a platform-specific way for the app to bring another instance to the front, implement this.
        /// This is an optional implementation as we have a second way to bring up client GUIs if this fails.
        /// </summary>
        void DisplayExistingUI();
    }
}
