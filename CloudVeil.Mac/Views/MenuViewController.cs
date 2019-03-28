using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using AppKit;
using CloudVeilGUI.ViewModels;
using CloudVeilGUI.Models;
using CloudVeil.Mac.Delegates;
using CloudVeil.Mac.DataSources;

namespace CloudVeil.Mac.Views
{
    public partial class MenuViewController : AppKit.NSViewController
    {
    
        MenuViewModel viewModel;

        #region Constructors

        // Called when created from unmanaged code
        public MenuViewController(IntPtr handle) : base(handle)
        {
            Initialize();
        }

        // Called when created directly from a XIB file
        [Export("initWithCoder:")]
        public MenuViewController(NSCoder coder) : base(coder)
        {
            Initialize();
        }

        // Call to load from the XIB/NIB file
        public MenuViewController() : base("MenuView", NSBundle.MainBundle)
        {
            Initialize();
        }

        // Shared initialization code
        void Initialize()
        {

        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            viewModel = ModelManager.Default.GetModel<MenuViewModel>();

            NSOutlineViewDataSource source = new MenuViewDataSource(viewModel);
            NSOutlineViewDelegate @delegate = new MenuViewDelegate();

            this.sidebarView.DataSource = source;
            this.sidebarView.Delegate = @delegate;
        }

        #endregion

    }
}
