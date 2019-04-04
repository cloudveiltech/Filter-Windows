using CloudVeilInstallerUI.Models;
using CloudVeilInstallerUI.ViewModels;
using CloudVeilInstallerUI.Views;
using MahApps.Metro.Controls;
using Microsoft.Tools.WindowsInstallerXml.Bootstrapper;
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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CloudVeilInstallerUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow, ISetupUI
    {
        private IInstallerViewModel viewModel;

        public MainWindow(IInstallerViewModel viewModel, bool showPrompts)
        {
            this.viewModel = viewModel;
            this.viewModel.ShowPrompts = showPrompts;

            InitializeComponent();
        }

        private IntPtr hwnd;
        public IntPtr Hwnd
        {
            get
            {
                if(hwnd == IntPtr.Zero)
                {
                    WindowInteropHelper h = new WindowInteropHelper(this);
                    hwnd = h.Handle;
                }

                return hwnd;
            }
        }

        public void LoadView(UserControl view)
        {
            view.DataContext = this.viewModel;
            this.contentControl.Content = view;
        }

        public void ShowInstall()
        {
            Dispatcher.InvokeAsync(() => LoadView(new InstallView(viewModel)));
        }

        public void ShowWelcome()
        {
            try
            {
                Dispatcher.InvokeAsync(() => LoadView(new WelcomeView(viewModel)));
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public void ShowLicense()
        {
            try
            {
                Dispatcher.InvokeAsync(() => LoadView(new LicenseView(viewModel)));
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public void ShowFinish()
        {
            Dispatcher.InvokeAsync(() => LoadView(new FinishedView(viewModel)));
        }
    }
}
