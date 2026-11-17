using CloudVeil.Windows;
using System.Windows.Controls;
using Gui.CloudVeil.UI.ViewModels;

namespace Gui.CloudVeil.UI.Views
{
    /// <summary>
    /// Interaction logic for CollectDiagnosticsView.xaml
    /// </summary>
    public partial class CollectDiagnosticsView : UserControl
    {
        public CollectDiagnosticsView()
        {
            InitializeComponent();

            DataContext = (CloudVeilApp.Current as CloudVeilApp).ModelManager.Get<CollectDiagnosticsViewModel>();
        }
    }
}
