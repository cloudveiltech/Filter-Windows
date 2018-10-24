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

namespace CloudVeil.Windows.Views
{
    /// <summary>
    /// Interaction logic for BlockedPagesPage.xaml
    /// </summary>
    public partial class BlockedPagesPage : WpfLightToolkit.Controls.LightContentPage
    {
        public BlockedPagesPage()
        {
            InitializeComponent();
            DataContext = new BlockedPagesViewModel(ModelManager.Default.GetModel<BlockedPagesModel>());
        }
    }
}
