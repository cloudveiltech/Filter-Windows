using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using CloudVeilGUI.Views;
using CloudVeilGUI.Platform.Common;
using Citadel.IPC;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace CloudVeilGUI
{
    public partial class App : Application
    {
        public IPCClient m_ipcClient;

        public App()
        {
            InitializeComponent();

            MainPage = new MainPage();
        }

        protected override void OnStart()
        {
            var filterStarter = PlatformServices.Default.CreateFilterStarter();

            filterStarter.StartFilter();

            m_ipcClient = IPCClient.InitDefault();
        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }
    }
}
