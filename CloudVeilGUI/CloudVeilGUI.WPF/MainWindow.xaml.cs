/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using Xamarin.Forms.Platform.WPF;
using Xamarin.Forms;

namespace CloudVeilGUI.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FormsApplicationPage
    {
        public MainWindow()
        {
            InitializeComponent();

            Forms.Init();

            var app = new CloudVeilGUI.App();
            LoadApplication(app);

            //((App)App.Current).SessionEnding += MainWindow_SessionEnding;
        }
    }
}
