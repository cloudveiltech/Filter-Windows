/*
* Copyright © 2017 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using Citadel.Core.Windows.Util;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

        public void ShowDisabledInternetMessage(DateTime restoreTime)
        {
            /*m_disabledInternetGrid.Visibility = Visibility.Visible;

            m_internetRestorationTimeLabel.Content = restoreTime.ToLongDateString() + " " + restoreTime.ToShortTimeString();*/
        }

        public void HideDisabledInternetMessage()
        {
            /*m_disabledInternetGrid.Visibility = Visibility.Hidden;*/
        }

        private void OnHyperlinkClicked(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Click_SidebarButton(object sender, RoutedEventArgs e)
        {
            Grid sidebar = (Grid)((ToggleButton)sender).Parent;
            ToggleButton button = sender as ToggleButton;

            foreach(var child in sidebar.Children)
            {
                if(child is ToggleButton && child != sender)
                {
                    ((ToggleButton)child).IsChecked = false;
                }
            }

            var buttonParameter = button.CommandParameter as string;
            
            switch(buttonParameter)
            {
                case "history":
                    this.CurrentView.Content = new HistoryView();
                    break;
            }
        }
    }
}