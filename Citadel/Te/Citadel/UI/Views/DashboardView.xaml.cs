using System;
using System.Windows;
using System.Windows.Controls;
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
            try
            {
                var selectedBlockEvent = (DashboardViewModel.ViewableBlockedRequests)m_blockEventsDataGrid.SelectedItem;

                if(selectedBlockEvent != null)
                {
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
                }
            }
            catch(Exception err)
            {
                LoggerUtil.RecursivelyLogException(m_logger, err);
            }
        }
    }
}