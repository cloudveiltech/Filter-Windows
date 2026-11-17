using CloudVeil.Windows;
using System.Diagnostics;
using System.Windows.Navigation;
using Gui.CloudVeil.UI.ViewModels;

namespace Gui.CloudVeil.UI.Views
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class SelfModerationView : BaseView
    {
        public SelfModerationView()
        {
            DataContext = (CloudVeilApp.Current as CloudVeilApp).ModelManager.Get<SelfModerationViewModel>();
            InitializeComponent();
        }

        private void OnHyperlinkClicked(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
