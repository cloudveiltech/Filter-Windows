using CloudVeil.Windows;
using System.Diagnostics;
using System.Windows.Navigation;
using Gui.CloudVeil.UI.ViewModels;

namespace Gui.CloudVeil.UI.Views
{
    /// <summary>
    /// Interaction logic for TimeRestrictionsView.xaml
    /// </summary>
    public partial class TimeRestrictionsView : BaseView
    {
        public TimeRestrictionsView()
        {
            InitializeComponent();
            DataContext = (CloudVeilApp.Current as CloudVeilApp).ModelManager.Get<TimeRestrictionsViewModel>();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
