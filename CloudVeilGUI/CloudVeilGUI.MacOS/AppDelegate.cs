using System;
using AppKit;
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

        private void eventHandler(object sender, EventArgs eventArgs)
        {
            
        }

        public override void WillTerminate(NSNotification notification)
        {
            // Insert code here to tear down your application
        }
    }
}
