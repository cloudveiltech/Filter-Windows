// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using CloudVeilGUI.Models;
using CloudVeilGUI.ViewModels;
using Foundation;

namespace CloudVeil.Mac.DataSources
{
    public class ProxyMenuItem : NSObject
    {
        public ProxyMenuItem(HomeMenuItem item)
        {
            this.item = item;
        }

        private HomeMenuItem item;

        public MenuItemType Id { get => item.Id; }
        public string Title { get => item.Title; }
    }

    public class MenuViewDataSource : AppKit.NSOutlineViewDataSource
    {
        public MenuViewDataSource(MenuViewModel viewModel)
        {
            this.viewModel = viewModel;
        }

        private MenuViewModel viewModel;

        [Export("outlineView:isItemExpandable:")]
        public override bool ItemExpandable(AppKit.NSOutlineView outlineView, Foundation.NSObject item)
        {
            return false;
        }

        [Export("outlineView:numberOfChildrenOfItem:")]
        public override nint GetChildrenCount(AppKit.NSOutlineView outlineView, NSObject item)
        {
            return this.viewModel.MenuItems.Count;
        }

        [Export("outlineView:child:ofItem:")]
        public override NSObject GetChild(AppKit.NSOutlineView outlineView, nint childIndex, NSObject item)
        {
            return new ProxyMenuItem(viewModel.MenuItems[(int)childIndex]);
        }
    }
}
