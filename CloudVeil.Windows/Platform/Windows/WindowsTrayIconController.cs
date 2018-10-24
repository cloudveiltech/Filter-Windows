/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using CloudVeilGUI.Platform.Common;
using Filter.Platform.Common;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;

namespace CloudVeilGUI.Platform.Windows
{
    public class WindowsTrayIconController : ITrayIconController
    {
        NotifyIcon trayIcon;
        IGuiServices guiServices;

        public void InitializeIcon(List<StatusIconMenuItem> menuItems)
        {
            if(trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }

            trayIcon = new System.Windows.Forms.NotifyIcon();
            guiServices = PlatformTypes.New<IGuiServices>();

            var iconPackUri = new Uri("pack://application:,,,/Resources/appicon.ico");
            var resourceStream = System.Windows.Application.GetResourceStream(iconPackUri);

            trayIcon.Icon = new System.Drawing.Icon(resourceStream.Stream);

            trayIcon.DoubleClick +=
                delegate (object sender, EventArgs args)
                {
                    guiServices.BringAppToFront();
                };

            trayIcon.BalloonTipClosed += delegate (object sender, EventArgs args)
            {
                trayIcon.Visible = true;
            };

            var windowsMenuItems = new List<System.Windows.Forms.MenuItem>();

            foreach(var item in menuItems)
            {
                if(item.IsSeparator)
                {
                    windowsMenuItems.Add(new MenuItem("-"));
                }
                else
                {
                    windowsMenuItems.Add(new MenuItem(item.ItemName, item.OnTriggered));
                }
            }

            trayIcon.ContextMenu = new ContextMenu(windowsMenuItems.ToArray());

            trayIcon.Visible = true;
        }

        public void DestroyIcon()
        {
            trayIcon.Visible = false;
            trayIcon.Dispose();

            trayIcon = null;
        }

        public void ShowNotification(string title, string message)
        {
            trayIcon.ShowBalloonTip(0, title, message, ToolTipIcon.Info);
        }
    }
}
