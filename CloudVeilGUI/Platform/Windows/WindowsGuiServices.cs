/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeilGUI.Platform.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Gui.CloudVeil.UI.Views;

namespace CloudVeil.Windows.Platform
{
    public class WindowsGuiServices : IGuiServices
    {
        private Application app;
        public WindowsGuiServices()
        {
           app = Application.Current;
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
                });
        }

        public void DisplayAlert(string title, string message, string okButton)
        {
            app.Dispatcher.BeginInvoke((Action)delegate ()
            {
                // TODO: DisplayAlert
            }, DispatcherPriority.Normal);   
        }

        public void ShowCooldownEnforcementScreen()
        {
            Console.WriteLine("Cooldown enforcement not implemented.");
            return;
        }

        public void ShowLoginScreen()
        {
        }

        public void ShowWaitingScreen()
        {
        
        }
        
        public void ShowMainScreen()
        {
        }

        public void ShowCertificateErrorsScreen()
        {
            throw new NotImplementedException();
        }
    }
}
