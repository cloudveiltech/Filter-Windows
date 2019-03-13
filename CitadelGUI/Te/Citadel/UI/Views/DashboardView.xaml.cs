/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Util;
using Filter.Platform.Common.Util;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Te.Citadel.UI.ViewModels;

namespace Te.Citadel.UI.Views
{
    /// <summary>
    /// Interaction logic for DashboardView.xaml 
    /// </summary>
    public partial class DashboardView : BaseView
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        private void OnHyperlinkClicked(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        public void SwitchTab(int tabIdx)
        {
            MessageBox.Show("Invalid command. Please tell the developer to re-implement SwitchTab");

            //Dispatcher.BeginInvoke((Action)(() => tabControl.SelectedIndex = tabIdx));
        }

        private void MenuControl_ItemInvoked(object sender, MahApps.Metro.Controls.HamburgerMenuItemInvokedEventArgs e)
        {
            this.MenuControl.Content = e.InvokedItem;
        }
    }
}