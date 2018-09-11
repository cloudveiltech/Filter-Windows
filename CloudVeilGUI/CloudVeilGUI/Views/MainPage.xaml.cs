using CloudVeilGUI.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace CloudVeilGUI.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class MainPage : MasterDetailPage
    {
        Dictionary<int, NavigationPage> MenuPages = new Dictionary<int, NavigationPage>();
        public MainPage()
        {
            InitializeComponent();

            MasterBehavior = MasterBehavior.Split;
            
            // Not sure whats going on here?
            MenuPages.Add((int)MenuItemType.BlockedPages, (NavigationPage)Detail);
        }

        public async Task NavigateFromMenu(int id)
        {
            if (!MenuPages.ContainsKey(id))
            {
                switch (id)
                {
                    case (int)MenuItemType.BlockedPages:
                        MenuPages.Add(id, new NavigationPage(new BlockedPagesPage()));
                        break;

                    case (int)MenuItemType.SelfModeration:
                        MenuPages.Add(id, new NavigationPage(new SelfModerationPage()));
                        break;

                    case (int)MenuItemType.TimeRestrictions:
                        MenuPages.Add(id, new NavigationPage(new TimeRestrictionsPage()));
                        break;

                    case (int)MenuItemType.RelaxedPolicy:
                        MenuPages.Add(id, new NavigationPage(new RelaxedPolicyContentPage()));
                        break;

                    case (int)MenuItemType.Advanced:
                        MenuPages.Add(id, new NavigationPage(new AdvancedPage()));
                        break;

                    case (int)MenuItemType.Support:
                        MenuPages.Add(id, new NavigationPage(new SupportPage()));
                        break;

                    case (int)MenuItemType.Diagnostics:
                        MenuPages.Add(id, new NavigationPage(new DiagnosticsPage()));
                        break;
                }
            }

            var newPage = MenuPages[id];

            if (newPage != null && Detail != newPage)
            {
                Detail = newPage;

                if (Device.RuntimePlatform == Device.Android)
                    await Task.Delay(100);
            }
        }
    }
}