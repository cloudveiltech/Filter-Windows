/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeil.Core.Windows.Util;
using Filter.Platform.Common.Util;
using MahApps.Metro.Controls;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Gui.CloudVeil.UI.ViewModels;

namespace Gui.CloudVeil.UI.Views
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

        public void SwitchTab(Type tabType)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                var itemsSource = MenuControl.ItemsSource as HamburgerMenuItemCollection;

                if (itemsSource != null)
                {
                    for (int i = 0; i < itemsSource.Count; i++)
                    {
                        HamburgerMenuItem item = itemsSource[i];

                        if (tabType.IsAssignableFrom(item.Tag.GetType()))
                        {
                            MenuControl.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }));
        }

        private void MenuControl_ItemInvoked(object sender, MahApps.Metro.Controls.HamburgerMenuItemInvokedEventArgs e)
        {
            this.MenuControl.Content = e.InvokedItem;
        }
    }
}