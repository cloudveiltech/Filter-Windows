using System;
using System.Windows;
using System.Windows.Controls;
using Te.Citadel.UI.ViewModels;

namespace Te.Citadel.UI.Views
{
    /// <summary>
    /// Interaction logic for DashboardView.xaml
    /// </summary>
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        public void AppendBlockActionEvent(string category, string fullRequest)
        {
            // Keep number of items truncated to 50.
            if(m_blockEventsDataGrid.Items.Count > 50)
            {
                m_blockEventsDataGrid.Items.RemoveAt(0);
            }

            // Add the item to view.
            m_blockEventsDataGrid.Items.Add(new DashboardViewModel.ViewableBlockedRequests(category, fullRequest));
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

        private void OnRequestReviewBlockActionClicked(object sender, RoutedEventArgs e)
        {
            var selectedBlockEvent = (DashboardViewModel.ViewableBlockedRequests)m_blockEventsDataGrid.SelectedItem;
        }
    }
}