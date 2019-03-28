// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace CloudVeil.Mac.Views
{
	[Register ("BlockedPagesViewController")]
	partial class BlockedPagesViewController
	{
		[Outlet]
		AppKit.NSTableView blockedPagesTable { get; set; }

		[Action ("privacyPolicy_Click:")]
		partial void privacyPolicy_Click (Foundation.NSObject sender);

		[Action ("requestReview_Click:")]
		partial void requestReview_Click (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (blockedPagesTable != null) {
				blockedPagesTable.Dispose ();
				blockedPagesTable = null;
			}
		}
	}
}
