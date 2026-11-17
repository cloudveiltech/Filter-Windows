/*
* Copyright (c) 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using CloudVeil.Windows;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Gui.CloudVeil.UI.ViewModels;

namespace Gui.CloudVeil.UI.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class RelaxedPolicyView : BaseView
    {
        public RelaxedPolicyView()
        {
            InitializeComponent();
            DataContext = (CloudVeilApp.Current as CloudVeilApp).ModelManager.Get<RelaxedPolicyViewModel>();
        }

        public void ShowDisabledInternetMessage(DateTime restoreTime)
        {
            disabledInternetGrid.Visibility = Visibility.Visible;

            internetRestorationTimeLabel.Content = restoreTime.ToLongDateString() + " " + restoreTime.ToShortTimeString();
        }

        public void HideDisabledInternetMessage()
        {
            disabledInternetGrid.Visibility = Visibility.Hidden;
        }

        private void OnHyperlinkClicked(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
