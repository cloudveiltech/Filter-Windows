using CloudVeilGUI.Platform.Common;
using CloudVeilGUI.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CloudVeilGUI.Platform.Windows
{
    public class WindowsGuiServices : IGuiServices
    {
        private WPFApp app;
        public WindowsGuiServices()
        {
           app = (WPFApp)Application.Current;
        }

        public void BringAppToFront()
        {
            this.app.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,
                (Action)delegate ()
                {
                    if(app.MainWindow != null)
                    {
                        app.MainWindow.Show();
                        app.MainWindow.WindowState = WindowState.Normal;
                        app.MainWindow.Topmost = true;
                        app.MainWindow.Topmost = false;
                    }

                    // TODO: Implement tray icon.
                });
        }
    }
}
