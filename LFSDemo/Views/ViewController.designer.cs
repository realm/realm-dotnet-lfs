// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace LFSDemo
{
	[Register ("ViewController")]
	partial class ViewController
	{
		[Outlet]
		UIKit.UITableView FeedTableView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (FeedTableView != null) {
				FeedTableView.Dispose ();
				FeedTableView = null;
			}
		}
	}
}
