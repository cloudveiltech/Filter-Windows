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
    /// Interaction logic for FinishedView.xaml
    /// </summary>
    public partial class FinishedView : UserControl
    {
        public FinishedView(IInstallerViewModel viewModel)
        {
            DataContext = viewModel;
            this.viewModel = viewModel;

            InitializeComponent();
        }

        private IInstallerViewModel viewModel;

        private void Exit(object sender, RoutedEventArgs e)
        {
            viewModel.Exit();
        }

        private void RestartComputer(object sender, RoutedEventArgs e)
        {
            try
            {
                RebootMethods.Reboot(ShutdownReason.MajorApplication | ShutdownReason.MinorInstallation | ShutdownReason.FlagPlanned);
            }
            catch (Exception)
            {
                viewModel.FinishedMessage = "Failed to restart the computer. Please restart manually before trying again.";
            }
        }
    }
}
