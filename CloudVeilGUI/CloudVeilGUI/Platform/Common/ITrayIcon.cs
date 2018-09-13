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

    /// <summary>
    /// Implementing a cross-platform tray icon controller is rather difficult because of the UI/UX differences between macOS and Windows.
    /// For instance, we might want to always show the status bar icon on macOS, but only show it on Windows when the GUI is gone.
    /// </summary>
    public interface ITrayIconController
    {
        /// <summary>
        /// Initializes and shows the status bar/tray icon.
        /// </summary>
        /// <param name="menuItems">A list of menu items and associated EventHandlers for those icons.</param>
        void InitializeIcon(List<StatusIconMenuItem> menuItems);

        /// <summary>
        /// Destroys the icon and releases its resources.
        /// </summary>
        void DestroyIcon();

        /// <summary>
        /// This should show a notification with the specified title and message for however long the timeout is specified for.
        /// Should also be user-dismissable.
        /// </summary>
        /// <param name="title">The title of the notification. Can be null.</param>
        /// <param name="message">The message of the notification. Must have contents.</param>
        void ShowNotification(string title, string message);
    }
}
