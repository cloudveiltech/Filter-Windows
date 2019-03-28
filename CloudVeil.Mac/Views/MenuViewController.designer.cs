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
	[Register ("MenuViewController")]
	partial class MenuViewController
	{
		[Outlet]
		AppKit.NSOutlineView sidebarView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (sidebarView != null) {
				sidebarView.Dispose ();
				sidebarView = null;
			}
		}
	}
}
