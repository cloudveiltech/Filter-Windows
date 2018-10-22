/*
* Copyright © 2017-2018 Cloudveil Technology Inc.
* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

﻿using CloudVeilGUI.Models;
using System;
using System.Collections.Generic;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace CloudVeilGUI.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class MenuPage : ContentPage
    {
        MainPage RootPage { get => (Application.Current.MainPage as NavigationPage).RootPage as MainPage; }
        List<HomeMenuItem> menuItems;
        public MenuPage()
        {
            InitializeComponent();

            menuItems = new List<HomeMenuItem>
            {
                new HomeMenuItem { Id = MenuItemType.BlockedPages, Title = "Blocked Pages" },
                new HomeMenuItem { Id = MenuItemType.SelfModeration, Title = "Self-moderation" },
                new HomeMenuItem { Id = MenuItemType.TimeRestrictions, Title = "Time Restrictions" },
                new HomeMenuItem { Id = MenuItemType.RelaxedPolicy, Title = "Relaxed Policy" },
                new HomeMenuItem { Id = MenuItemType.Advanced, Title = "Advanced" },
                new HomeMenuItem { Id = MenuItemType.Support, Title = "Support" },
                new HomeMenuItem { Id = MenuItemType.Diagnostics, Title = "Diagnostics" }
            };

            ListViewMenu.ItemsSource = menuItems;

            ListViewMenu.SelectedItem = menuItems[0];
            ListViewMenu.ItemSelected += async (sender, e) =>
            {
                if (e.SelectedItem == null)
                    return;

                var id = (int)((HomeMenuItem)e.SelectedItem).Id;
                await RootPage.NavigateFromMenu(id);
            };
        }
    }
}