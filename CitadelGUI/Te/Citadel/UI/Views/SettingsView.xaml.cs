/*
* Copyright (c) 2018 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using CloudVeil.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Te.Citadel.UI.ViewModels;

namespace Te.Citadel.UI.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : BaseView
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContext = (CitadelApp.Current as CitadelApp).ModelManager.Get<SettingsViewModel>();
        }

        public void ShowDisabledInternetMessage(DateTime restoreTime)
        {
            m_disabledInternetGrid.Visibility = Visibility.Visible;

            m_internetRestorationTimeLabel.Content = restoreTime.ToLongDateString() + " " + restoreTime.ToShortTimeString();
        }

        public void HideDisabledInternetMessage()
        {
            m_disabledInternetGrid.Visibility = Visibility.Hidden;
        }
    }
}
