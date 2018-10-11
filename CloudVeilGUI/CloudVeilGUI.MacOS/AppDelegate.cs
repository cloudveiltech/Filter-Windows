/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
﻿using AppKit;
using CloudVeilGUI.MacOS.Platform;
using CloudVeilGUI.Platform.Common;
using Filter.Platform.Common;
using Foundation;
using Xamarin.Forms;
using Xamarin.Forms.Platform.MacOS;

namespace CloudVeilGUI.MacOS
{
    [Register("AppDelegate")]
    public class AppDelegate : FormsApplicationDelegate
    {
        NSWindow window;

        public AppDelegate()
        {
            var style = NSWindowStyle.Closable | NSWindowStyle.Resizable | NSWindowStyle.Titled;

            var rect = new CoreGraphics.CGRect(200, 200, 1024, 768);
            window = new NSWindow(rect, style, NSBackingStore.Buffered, false);
            window.Title = "CloudVeil";
            window.TitleVisibility = NSWindowTitleVisibility.Hidden;
        }

        public override NSWindow MainWindow => window;

        private NSStatusItem statusItem;

        public override void DidFinishLaunching(NSNotification notification)
        {
            Filter.Platform.Mac.Platform.Init();
            PlatformTypes.Register<ITrayIconController>((arr) => new MacTrayIconController());
            PlatformTypes.Register<IGuiServices>((arr) => new MacGuiServices());
            PlatformTypes.Register<IFilterStarter>((arr) => new MacFilterStarter());

            Forms.Init();
            LoadApplication(new CloudVeilGUI.App(true));

            float width = 30.0f;
            var item = NSStatusBar.SystemStatusBar.CreateStatusItem(width);

            var button = item.Button;
            button.Image = NSImage.ImageNamed("StatusBarButtonImage");

            var menu = new NSMenu();
            menu.AddItem(new NSMenuItem("Open", "o", eventHandler));
            menu.AddItem(NSMenuItem.SeparatorItem);
            menu.AddItem(new NSMenuItem("Settings", "s", eventHandler));
            menu.AddItem(new NSMenuItem("Use Relaxed Policy", "p", eventHandler));

            item.Menu = menu;

            statusItem = item;

            base.DidFinishLaunching(notification);
        }

        public override void WillTerminate(NSNotification notification)
        {
            // Insert code here to tear down your application
        }
    }
}
