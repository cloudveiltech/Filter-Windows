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

        public void AppendBlockActionEvent(string message)
        {
            m_blockEventsTextBox.Text = message + Environment.NewLine + m_blockEventsTextBox.Text;
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
