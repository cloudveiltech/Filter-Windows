/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿namespace CloudVeilGUI.Platform.Common
{
    /// <summary>
    /// This is a custom model class that should represent a cross-platform menu entry.
    /// </summary>
    public class StatusIconMenuItem
    {
        public static readonly StatusIconMenuItem Separator = new StatusIconMenuItem(true);

        public StatusIconMenuItem(bool isSeparator)
        {
            IsSeparator = isSeparator;
        }

        public StatusIconMenuItem(string itemName, System.EventHandler handler) : this(itemName, null, handler)
        {

        }

        public StatusIconMenuItem(string itemName, string charCode, System.EventHandler handler)
        {
            ItemName = itemName;
            CharCode = charCode;

            Triggered += handler;
        }

        /// <summary>
        /// Determines whether the specified menu item should be rendered as a separator.
        /// </summary>
        public bool IsSeparator { get; set; }

        /// <summary>
        /// The name of the menu item.
        /// </summary>
        public string ItemName { get; set; }

        /// <summary>
        /// Character code for this menu item, or null if you don't want any.
        /// </summary>
        public string CharCode { get; set; }

        /// <summary>
        /// The event that gets called when the item gets clicked on or otherwise triggered.
        /// </summary>
        public event System.EventHandler Triggered;

        public void OnTriggered(object sender, System.EventArgs args)
        {
            Triggered?.Invoke(sender, args);
        }
    }
}
