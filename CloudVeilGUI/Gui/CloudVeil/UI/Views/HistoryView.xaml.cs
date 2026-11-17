/*
* Copyright © 2019 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using CloudVeil.Windows;
using System.Windows;
using System.Windows.Controls;
using Gui.CloudVeil.UI.ViewModels;

namespace Gui.CloudVeil.UI.Views
{
    /// <summary>
    /// Interaction logic for HistoryView.xaml
    /// </summary>
    public partial class HistoryView : BaseView
    {
        public HistoryView()
        {
            InitializeComponent();

            DataContext = (CloudVeilApp.Current as CloudVeilApp).ModelManager.Get<HistoryViewModel>();

            NameScope.SetNameScope(contextMenu, NameScope.GetNameScope(this));
        }

        public override ScrollBarVisibility ShouldViewScroll => ScrollBarVisibility.Disabled;
    }
}
