/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeil.Windows.Views;
using CloudVeilGUI.Platform.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using WpfLightToolkit.Controls;

namespace CloudVeilGUI.Platform.Windows
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
                if (app.MainWindow is LightWindow)
                {
                    var window = app.MainWindow as LightWindow;

                    var modal = new Modal(window, title, message, okButton);
                    window.PushModal(modal);

                }
            }, DispatcherPriority.Normal);   
        }

        public void ShowCooldownEnforcementScreen()
        {
            Console.WriteLine("Cooldown enforcement not implemented.");
            return;
        }

        public void ShowLoginScreen()
        {
            app.Dispatcher.BeginInvoke((Action)delegate ()
            {
                if(app.MainWindow is LightWindow)
                {
                    var window = app.MainWindow as LightWindow;

                    window.StartupPage = (window.StartupPage is LoginPage) ? window.StartupPage : new LoginPage();
                }
            });
        }

        public void ShowWaitingScreen()
        {
            app.Dispatcher.BeginInvoke((Action)delegate ()
            {
                if(app.MainWindow is LightWindow)
                {
                    var window = app.MainWindow as LightWindow;

                    var waitingPage = new WaitingPage();
                    window.StartupPage = (window.StartupPage is WaitingPage) ? window.StartupPage : new WaitingPage();
                }
            });
        }

        private MainPage mainPage;
        public void ShowMainScreen()
        {
            app.Dispatcher.BeginInvoke((Action)delegate ()
            {
                if(app.MainWindow is LightWindow)
                {
                    var window = app.MainWindow as LightWindow;

                    if(window.StartupPage is MainPage && mainPage != null)
                    {
                        mainPage = window.StartupPage as MainPage;
                    }

                    window.StartupPage = (window.StartupPage is MainPage) ? window.StartupPage : (mainPage ?? (mainPage = new MainPage()));
                    window.StartupPage = mainPage;
                }
            });
        }

        public void ShowCertificateErrorsScreen()
        {
            throw new NotImplementedException();
        }
    }
}
