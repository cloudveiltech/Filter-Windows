using CloudVeilGUI.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace CloudVeilGUI.ViewModels
{
    public class MenuViewModel
    {

        public List<HomeMenuItem> MenuItems { get; private set; }

        public MenuViewModel()
        {
            MenuItems = new List<HomeMenuItem>
            {
                new HomeMenuItem {Id = MenuItemType.BlockedPages, Title = "Blocked Pages" },
                new HomeMenuItem { Id = MenuItemType.SelfModeration, Title = "Self-moderation" },
                new HomeMenuItem { Id = MenuItemType.TimeRestrictions, Title = "Time Restrictions" },
                new HomeMenuItem { Id = MenuItemType.RelaxedPolicy, Title = "Relaxed Policy" },
                new HomeMenuItem { Id = MenuItemType.Advanced, Title = "Advanced" },
                new HomeMenuItem { Id = MenuItemType.Support, Title = "Support" },
                new HomeMenuItem { Id = MenuItemType.Diagnostics, Title = "Diagnostics" }
            };
        }
    }
}
