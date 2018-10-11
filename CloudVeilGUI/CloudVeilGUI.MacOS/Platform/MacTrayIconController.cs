// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using System.Collections.Generic;
using CloudVeilGUI.Platform.Common;
using AppKit;
using UserNotifications;
using Filter.Platform.Common.Util;

namespace CloudVeilGUI.MacOS.Platform
{
    public class MacTrayIconController : ITrayIconController
    {
        public MacTrayIconController()
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        private NLog.Logger logger;

        private NSStatusItem statusItem;
        private bool isGranted;

        public void DestroyIcon()
        {
            statusItem.Visible = false;
            NSStatusBar.SystemStatusBar.RemoveStatusItem(statusItem);
        }

        public void InitializeIcon(List<StatusIconMenuItem> menuItems)
        {

            requestNotificationAuth((granted, nsError) =>
            {
                isGranted = granted;
            });

            float width = 30.0f;
            var item = NSStatusBar.SystemStatusBar.CreateStatusItem(width);

            var button = item.Button;
            button.Image = NSImage.ImageNamed("StatusBarButtonImage");

            var menu = new NSMenu();

            foreach (var menuItem in menuItems)
            {
                if (menuItem.IsSeparator)
                {
                    menu.AddItem(NSMenuItem.SeparatorItem);
                }
                else
                {
                    menu.AddItem(new NSMenuItem(menuItem.ItemName, menuItem.CharCode, menuItem.OnTriggered));
                }
            }

            item.Menu = menu;
            statusItem = item;
        }

        private void requestNotificationAuth(Action<bool, Foundation.NSError> completionHandler)
        {
            UNUserNotificationCenter.Current.RequestAuthorization(UNAuthorizationOptions.Alert, completionHandler);
        }

        private void showNotificationWithNoChecks(string title, string message)
        {
            UNMutableNotificationContent content = new UNMutableNotificationContent()
            {
                Title = title,
                Body = message
            };

            UNNotificationRequest request = UNNotificationRequest.FromIdentifier("org.cloudveil.cloudveilformac", content, null);
            UNUserNotificationCenter.Current.AddNotificationRequest(request, (nsError) =>
            {
                // Do nothing
            });
        }

        public void ShowNotification(string title, string message)
        {
            UNUserNotificationCenter.Current.GetNotificationSettings((settings) =>
            {
                switch (settings.AuthorizationStatus)
                {
                    case UNAuthorizationStatus.Authorized:
                    case UNAuthorizationStatus.Provisional:
                        showNotificationWithNoChecks(title, message);
                        break;

                    case UNAuthorizationStatus.NotDetermined:
                        requestNotificationAuth((granted, error) =>
                        {
                            if (error == null && granted)
                            {
                                showNotificationWithNoChecks(title, message);
                            }
                        });
                        break;

                    default:
                        logger.Warn("Failed to deliver notification due to denied authorization.");
                        break;
                }
            });

            UNUserNotificationCenter.Current.RequestAuthorization(UNAuthorizationOptions.Alert, (granted, nsError) =>
            {
                if(nsError == null && granted)
                {

                }
            });
        }
    }
}
