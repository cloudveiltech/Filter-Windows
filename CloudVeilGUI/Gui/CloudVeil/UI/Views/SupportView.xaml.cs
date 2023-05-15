/*
* Copyright © 2019 Cloudveil Technology Inc.  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/
using CloudVeil.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Gui.CloudVeil.UI.ViewModels;
using System.Windows.Forms;

namespace Gui.CloudVeil.UI.Views
{
    /// <summary>
    /// Interaction logic for SupportView.xaml
    /// </summary>
    public partial class SupportView : BaseView
    {
        public SupportView()
        {
            InitializeComponent();

            DataContext = (CloudVeilApp.Current as CloudVeilApp).ModelManager.Get<SupportViewModel>();
        }

        private void OnHyperlinkClicked(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }


        public void Identifier_Mouse_Down(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Clipboard.SetText((DataContext as SupportViewModel).ActivationIdentifier);
        }
    }
}
