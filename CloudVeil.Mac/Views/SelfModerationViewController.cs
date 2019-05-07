using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using AppKit;

namespace CloudVeil.Mac.Views
{
    public partial class SelfModerationViewController : AppKit.NSViewController
    {
        #region Constructors

        // Called when created from unmanaged code
        public SelfModerationViewController(IntPtr handle) : base(handle)
        {
            Initialize();
        }

        // Called when created directly from a XIB file
        [Export("initWithCoder:")]
        public SelfModerationViewController(NSCoder coder) : base(coder)
        {
            Initialize();
        }

        // Call to load from the XIB/NIB file
        public SelfModerationViewController() : base("SelfModerationViewController", NSBundle.MainBundle)
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
