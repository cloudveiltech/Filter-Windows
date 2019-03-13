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
using Te.Citadel.UI.ViewModels;

namespace Te.Citadel.UI.Views
{
    /// <summary>
    /// Interaction logic for SoftwareConflictView.xaml
    /// </summary>
    public partial class SoftwareConflictView : BaseView
    {
        public static int ModalZIndex = 100;

        public SoftwareConflictView()
        {
            InitializeComponent();
            DataContext = (CitadelApp.Current as CitadelApp).ModelManager.Get<SoftwareConflictViewModel>();
        }

        private void OnNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
