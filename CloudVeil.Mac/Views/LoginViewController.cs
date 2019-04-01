using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using AppKit;

namespace CloudVeil.Mac.Views
{
    public partial class LoginViewController : AppKit.NSViewController
    {
        #region Constructors

        // Called when created from unmanaged code
        public LoginViewController(IntPtr handle) : base(handle)
        {
            Initialize();
        }

        // Called when created directly from a XIB file
        [Export("initWithCoder:")]
        public LoginViewController(NSCoder coder) : base(coder)
        {
            Initialize();
        }

        // Call to load from the XIB/NIB file
        public LoginViewController() : base("LoginView", NSBundle.MainBundle)
        {
            Initialize();
        }

        // Shared initialization code
        void Initialize()
        {
        }

        #endregion

    }
}
