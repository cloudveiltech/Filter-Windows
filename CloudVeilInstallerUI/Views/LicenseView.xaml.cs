using CloudVeilInstallerUI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// Interaction logic for LicenseView.xaml
    /// </summary>
    public partial class LicenseView : UserControl
    {
        public LicenseView(IInstallerViewModel model)
        {
            InitializeComponent();

            DataContext = model;
            viewModel = model;

            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();

                using (var stream = assembly.GetManifestResourceStream("CloudVeilInstallerUI.Resources.MOZILLA_PUBLIC_LICENSE.rtf"))
                {
                    LicenseBox.SelectAll();
                    LicenseBox.Selection.Load(stream, DataFormats.Rtf);
                }
            }
            catch
            {
                this.LicenseBox.Document.Blocks.Clear();
                this.LicenseBox.AppendText("Failed to load license file. A copy of the license file may be found at https://www.mozilla.org/en-US/MPL/2.0/");
            }
        }

        private IInstallerViewModel viewModel;

        private void TriggerWelcome(object sender, RoutedEventArgs e)
        {
            viewModel.TriggerWelcome();
        }

        private void TriggerInstall(object sender, RoutedEventArgs e)
        {
            viewModel.TriggerInstall();
        }
    }
}
