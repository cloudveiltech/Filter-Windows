// Copyright © 2018 CloudVeil Technology, Inc.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
using System;
using AppKit;
using CloudVeil.Mac.DataSources;
using Foundation;

namespace CloudVeil.Mac.Delegates
{
    public class MenuViewDelegate : NSOutlineViewDelegate
    {
        public MenuViewDelegate()
        {
        }

        [Export("outlineView:viewForTableColumn:item:")]
        public override NSView GetView(NSOutlineView outlineView, NSTableColumn tableColumn, NSObject item)
        {
            NSTableCellView view = null;

            var menuItem = item as ProxyMenuItem;

            if (menuItem != null)
            {
                view = outlineView.MakeView("MenuCell", this) as NSTableCellView;

                if (view?.TextField != null)
                {
                    view.TextField.StringValue = menuItem.Title;

                }
            }

            return view;
        }
    }
}
