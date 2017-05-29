/*
* Copyright © 2017 Jesse Nicholson  
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using System;
using System.Windows;
using System.Windows.Controls;
using Te.Citadel.Extensions;
using Te.Citadel.UI.Models;
using Te.Citadel.UI.ViewModels;
using Te.Citadel.Util;

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
                dataCtx.BlockEvents.Add(new DashboardViewModel.ViewableBlockedRequests(category, fullRequest));
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

        private async void OnRequestReviewBlockActionClicked(object sender, RoutedEventArgs e)
        {
            // XXX TODO - Having this code in here is sloppy. Clean this up.
            
            try
            {

                var selectedBlockEvent = (DashboardViewModel.ViewableBlockedRequests)m_blockEventsDataGrid.SelectedItem;
                

                if(selectedBlockEvent != null)
                {
#if UNBLOCK_REQUESTS_IN_BROWSER

                    // Try to send the device name as well. Helps distinguish between clients under the
                    // same account.
                    string deviceName = string.Empty;

                    try
                    {
                        deviceName = Environment.MachineName;
                    }
                    catch
                    {
                        deviceName = "Unknown";
                    }

                    var reportPath = WebServiceUtil.GetServiceProviderExternalUnblockRequestPath();
                    reportPath = string.Format(@"{0}?category_name={1}&user_id={2}&device_name={3}&blocked_request={4}", reportPath, Uri.EscapeDataString(selectedBlockEvent.CategoryName), Uri.EscapeDataString(AuthenticatedUserModel.Instance.Username), Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(deviceName)), Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(selectedBlockEvent.FullRequest)));
                    System.Diagnostics.Process.Start(reportPath);
#else
                    var formData = System.Text.Encoding.UTF8.GetBytes(string.Format("category={0}&full_request={1}", selectedBlockEvent.CategoryName, Uri.EscapeDataString(selectedBlockEvent.FullRequest)));
                    var result = await WebServiceUtil.SendResource("/capi/reportreview.php", formData, false);

                    if(result)
                    {
                        await DisplayDialogToUser("Success", "Thank you for your feedback. Your review request was successfully received.");
                    }
                    else
                    {
                        await DisplayDialogToUser("Error", "We were unable to receive your feedback. Please check your internet connection and if this issue persists, contact support.");
                    }
#endif
                }
            }
            catch(Exception err)
            {
                LoggerUtil.RecursivelyLogException(m_logger, err);
            }
        }
    }
}