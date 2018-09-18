/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using CloudVeilGUI.Platform.Common;
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
                });
        }
    }
}
