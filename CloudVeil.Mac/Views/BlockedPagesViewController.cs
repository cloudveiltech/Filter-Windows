using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using AppKit;
using CloudVeilGUI.Models;

namespace CloudVeil.Mac.Views
{
    public partial class BlockedPagesViewController : AppKit.NSViewController
    {
        #region Constructors

        // Called when created from unmanaged code
        public BlockedPagesViewController(IntPtr handle) : base(handle)
        {
            Initialize();
        }

        // Called when created directly from a XIB file
        [Export("initWithCoder:")]
        public BlockedPagesViewController(NSCoder coder) : base(coder)
        {
            Initialize();
        }

        // Call to load from the XIB/NIB file
        public BlockedPagesViewController() : base("BlockedPagesView", NSBundle.MainBundle)
        {
            Initialize();
        }

        // Shared initialization code
        void Initialize()
        {
        }

        BlockedPagesModel blockedPagesModel;

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            blockedPagesModel = ModelManager.Default.GetModel<BlockedPagesModel>();
            blockedPagesModel.BlockedPages.CollectionChanged += (sender, e) =>
            {
                BeginInvokeOnMainThread(() =>
                {
                    this.blockedPagesTable.ReloadData();
                });
            };

            this.blockedPagesTable.DataSource = new BlockedPagesDataSource(blockedPagesModel); 
        }
        #endregion

        partial void privacyPolicy_Click(NSObject sender)
        {
            throw new NotImplementedException();
        }

        partial void requestReview_Click(NSObject sender)
        {
            throw new NotImplementedException();
        }
    }

    public class BlockedPagesDelegate : NSTableViewDelegate
    {
        [Export("tableView:viewForTableColumn:row:")]
        public NSView GetViewForItem(NSTableView tableView, NSTableColumn tableColumn, nint row)
        {
            return null;
        }
    }

    public class BlockedPagesDataSource : NSTableViewDataSource
    {
        public BlockedPagesDataSource(BlockedPagesModel model)
        {
            this.model = model;
        }

        private BlockedPagesModel model;

        [Export("numberOfRowsInTableView:")]
        public override nint GetRowCount(NSTableView tableView)
        {
            return model.BlockedPages.Count;
        }

        [Export("tableView:objectValueForTableColumn:row:")]
        public override NSObject GetObjectValue(NSTableView tableView, NSTableColumn tableColumn, nint row)
        {
            switch(tableColumn.Identifier)
            {
                case "CategoryColumn":
                    return new NSString(model.BlockedPages[(int)row].CategoryName);
                case "UrlColumn":
                    return new NSString(model.BlockedPages[(int)row].FullRequestUri);
                default:
                    return null;
            }
        }
    }
}
