#define GUI_DEVELOPMENT

using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using CloudVeilGUI.Views;
using CloudVeilGUI.Platform.Common;
using Citadel.IPC;
using CloudVeilGUI.Models;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace CloudVeilGUI
{
    public partial class App : Application
    {
        private IPCClient m_ipcClient;

        public ModelManager ModelManager { get; private set; }

        public App()
        {
            InitializeComponent();

            ModelManager = new ModelManager();
            ModelManager.Register(new BlockedPagesModel());

            // Code smell: MainPage() makes use of ModelManager, so we need to instantiate ModelManager first.
            MainPage = new MainPage();
        }

        protected override void OnStart()
        {
            var filterStarter = PlatformServices.Default.CreateFilterStarter();

            filterStarter.StartFilter();

            m_ipcClient = IPCClient.InitDefault();

            m_ipcClient.BlockActionReceived = (args) =>
            {
                var blockedPagesModel = ModelManager.GetModel<BlockedPagesModel>();
                blockedPagesModel.BlockedPages.Add(new BlockedPageEntry(args.Category, args.Resource.ToString()));
            };
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
