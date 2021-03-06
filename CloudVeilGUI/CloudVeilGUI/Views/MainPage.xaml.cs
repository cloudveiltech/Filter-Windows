/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

using CloudVeilGUI.CustomFormElements;
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
        Dictionary<int, ModalHostPage> MenuPages = new Dictionary<int, ModalHostPage>();
        public MainPage()
        {
            InitializeComponent();

            MasterBehavior = MasterBehavior.Split;
            
            // Not sure whats going on here?
            MenuPages.Add((int)MenuItemType.BlockedPages, (ModalHostPage)Detail);
        }

        public async Task PushModal(Page modalPage)
        {
            if(Detail is ModalHostPage)
            {
                await (Detail as ModalHostPage).DisplayPageModal(modalPage);
            }
        }

        public async Task NavigateFromMenu(int id)
        {
            if (!MenuPages.ContainsKey(id))
            {
                switch (id)
                {
                    case (int)MenuItemType.BlockedPages:
                        MenuPages.Add(id, new ModalHostPage(new BlockedPagesPage()));
                        break;

                    case (int)MenuItemType.SelfModeration:
                        MenuPages.Add(id, new ModalHostPage(new SelfModerationPage()));
                        break;

                    case (int)MenuItemType.TimeRestrictions:
                        MenuPages.Add(id, new ModalHostPage(new TimeRestrictionsPage()));
                        break;

                    case (int)MenuItemType.RelaxedPolicy:
                        MenuPages.Add(id, new ModalHostPage(new RelaxedPolicyPage()));
                        break;

                    case (int)MenuItemType.Advanced:
                        MenuPages.Add(id, new ModalHostPage(new AdvancedPage()));
                        break;

                    case (int)MenuItemType.Support:
                        MenuPages.Add(id, new ModalHostPage(new SupportPage()));
                        break;

                    case (int)MenuItemType.Diagnostics:
                        MenuPages.Add(id, new ModalHostPage(new DiagnosticsPage()));
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