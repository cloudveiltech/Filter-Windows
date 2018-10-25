using CloudVeilGUI.Common;
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
    /// Interaction logic for MenuPage.xaml
    /// </summary>
    public partial class MenuPage : LightContentPage
    {
        private INavigation navigation;

        public MenuPage(INavigation navigation)
        {
            this.navigation = navigation;
            DataContext = ModelManager.Default.GetModel<MenuViewModel>();
            InitializeComponent();

            Menu.SelectionMode = SelectionMode.Single;

            Menu.SelectedIndex = 0;
            Menu.SelectionChanged += (sender, e) =>
            {
                if(e.AddedItems.Count == 0)
                {
                    return;
                }

                var id = ((HomeMenuItem)e.AddedItems[0]).Id;

                navigation.NavigateFromMenu(id);
            };

        }
    }
}
