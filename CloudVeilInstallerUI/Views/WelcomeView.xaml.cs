using CloudVeilInstallerUI.ViewModels;
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

namespace CloudVeilInstallerUI.Views
{
    /// <summary>
    /// Interaction logic for WelcomeView.xaml
    /// </summary>
    public partial class WelcomeView : UserControl
    {
        private IInstallerViewModel model;

        public WelcomeView(IInstallerViewModel model)
        {
            this.model = model;
            this.DataContext = model;

            InitializeComponent();
        }

        private void TriggerInstall(object sender, RoutedEventArgs e)
        {
            model.TriggerInstall();
        }

        private void TriggerLicense(object sender, RoutedEventArgs e)
        {
            if (model.InstallType == Models.InstallType.NewInstall)
            {
                model.TriggerLicense();
            }
            else
            {
                model.TriggerInstall();
            }
        }

        private void Exit(object sender, RoutedEventArgs e)
        {
            model.Exit();
        }
    }
}
