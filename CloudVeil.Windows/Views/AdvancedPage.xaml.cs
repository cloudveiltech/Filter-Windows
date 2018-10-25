using CloudVeil.Windows.ViewModels;
using CloudVeilGUI.Models;
using CloudVeilGUI.ViewModels;
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
using WpfLightToolkit.Controls;

namespace CloudVeil.Windows.Views
{
    /// <summary>
    /// Interaction logic for AdvancedPage.xaml
    /// </summary>
    public partial class AdvancedPage : LightContentPage
    {
        public AdvancedPage()
        {
            InitializeComponent();

            DataContext = new ProxyViewModel<AdvancedPageViewModel>(ModelManager.Default.GetModel<AdvancedPageViewModel>());
        }
    }
}
