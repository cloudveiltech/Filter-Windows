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

        public void AppendBlockActionEvent(string category, string fullRequest)
        {
            try
            {
                var dataCtx = (DashboardViewModel)this.DataContext;
                // Keep number of items truncated to 50.
                if(dataCtx.BlockEvents.Count > 50)
                {
                    dataCtx.BlockEvents.RemoveAt(0);
                }

                // Add the item to view.
                dataCtx.BlockEvents.Add(new ViewableBlockedRequests(category, fullRequest));
            }
            catch(Exception e)
            {
                LoggerUtil.RecursivelyLogException(m_logger, e);
            }
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

        private void OnHyperlinkClicked(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}