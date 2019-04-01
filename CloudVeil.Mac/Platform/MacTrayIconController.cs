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
using Foundation;

namespace CloudVeil.Mac.Platform
{
    interface INotificationController
    {
        void RequestAuthorization();

        void ShowNotification(string title, string message);
    }

    /// <summary>
    /// This class gets used in macOS 10.13 and lower because 10.14 is first with UNUserNotifications
    /// </summary>
    class NSUserNotificationsController : INotificationController
    {
        public void RequestAuthorization()
        {
            // No requesting of authorization needed.
        }

        public void ShowNotification(string title, string message)
        {
            var center = NSUserNotificationCenter.DefaultUserNotificationCenter;

            var notification = new NSUserNotification()
            {
                Title = title,
                InformativeText = message,
                DeliveryDate = NSDate.Now
            };

            center.ScheduleNotification(notification);
        }
    }

    class NewUserNotificationsController : INotificationController
    {
        public NewUserNotificationsController()
        {
            logger = LoggerUtil.GetAppWideLogger();
        }

        private NLog.Logger logger;

        private bool isGranted = false;

        public void RequestAuthorization()
        {
            requestNotificationAuth((isGranted, nsError) =>
            {
                isGranted = true;
            });
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
                if (nsError == null && granted)
                {

                }
            });
        }
    }

    public class MacTrayIconController : ITrayIconController
    {
        public MacTrayIconController()
        {
            logger = LoggerUtil.GetAppWideLogger();

            if(UNUserNotificationCenter.Current == null)
            {
                notificationController = new NSUserNotificationsController();
            }
            else
            {
                notificationController = new NewUserNotificationsController();
            }
        }

        private INotificationController notificationController;

        private NLog.Logger logger;

        private NSStatusItem statusItem;
        private bool isGranted;

        public void DestroyIcon()
        {
            if (statusItem != null)
            {
                statusItem.Visible = false;
                NSStatusBar.SystemStatusBar.RemoveStatusItem(statusItem);
            }
        }

        public void InitializeIcon(List<StatusIconMenuItem> menuItems)
        {
            notificationController.RequestAuthorization();

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
                    NSMenuItem nsMenuItem = null;

                    if(menuItem.CharCode == null)
                    {
                        nsMenuItem = new NSMenuItem(menuItem.ItemName, menuItem.OnTriggered);
                    }
                    else
                    {
                        nsMenuItem = new NSMenuItem(menuItem.ItemName, menuItem.CharCode, menuItem.OnTriggered);
                    }

                    menu.AddItem(nsMenuItem);
                }
            }

            item.Menu = menu;
            statusItem = item;
        }

        public void ShowNotification(string title, string message)
        {
            notificationController.ShowNotification(title, message);
        }
    }
}
