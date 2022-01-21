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
