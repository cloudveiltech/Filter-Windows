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
using CloudVeilGUI.Models;
using WpfLightToolkit.Controls;

namespace CloudVeil.Windows.Views
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class MainPage : LightMasterDetailPage, INavigation
    {
        public MainPage()
        {
            InitializeComponent();

            this.MasterPage = new MenuPage(this);
        }

        public void NavigateFromMenu(MenuItemType item)
        {
            switch(item)
            {
                case MenuItemType.BlockedPages:
                    this.DetailPage = new BlockedPagesPage();
                    break;

                default:
                    this.DetailPage = new BlockedPagesPage();
                    break;
            }
        }
    }
}
