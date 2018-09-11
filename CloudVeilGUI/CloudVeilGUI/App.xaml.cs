#define GUI_DEVELOPMENT

using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using CloudVeilGUI.Views;
using CloudVeilGUI.Platform.Common;
using Citadel.IPC;
using CloudVeilGUI.Models;
using Citadel.IPC.Messages;
using System.Collections;
using System.Collections.Generic;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]
namespace CloudVeilGUI
{
    public partial class App : Application
    {
        private IPCClient m_ipcClient;

        public ModelManager ModelManager { get; private set; }

        /// <summary>
        /// This is a stack for preserved pages in case one page needs to override another.
        /// </summary>
        protected Stack<Page> preservedPages;

        public App()
        {
            InitializeComponent();

            preservedPages = new Stack<Page>();

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

            m_ipcClient.AuthenticationResultReceived = (authenticationFailureResult) =>
            {
                switch (authenticationFailureResult.Action)
                {
                    case AuthenticationAction.Denied:
                    case AuthenticationAction.Required:
                    case AuthenticationAction.InvalidInput:
                        {
                            // User needs to log in.
                            //BringAppToFocus();

                            Device.BeginInvokeOnMainThread(() =>
                            {
                                if(!(MainPage is LoginPage))
                                {
                                    preservedPages.Push(MainPage);
                                    MainPage = new LoginPage();
                                }
                            });
                            var page = (MainPage)MainPage;

                            preservedPages.Push(page);

                            // TODO: User logged-in badge todo.

                            MainPage = new LoginPage();

                            /*m_mainWindow.Dispatcher.InvokeAsync(() =>
                            {
                                ((MainWindowViewModel)m_mainWindow.DataContext).IsUserLoggedIn = false;
                            });*/
                        }
                        break;

                    case AuthenticationAction.Authenticated:
                    case AuthenticationAction.ErrorNoInternet:
                    case AuthenticationAction.ErrorUnknown:
                        {
                            /*m_logger.Info($"The logged in user is {authenticationFailureResult.Username}");

                            m_mainWindow.Dispatcher.InvokeAsync(() =>
                            {
                                ((MainWindowViewModel)m_mainWindow.DataContext).LoggedInUser = authenticationFailureResult.Username;
                                ((MainWindowViewModel)m_mainWindow.DataContext).IsUserLoggedIn = true;
                            });

                            OnViewChangeRequest(typeof(DashboardView));*/
                        }
                        break;
                }
            };

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
