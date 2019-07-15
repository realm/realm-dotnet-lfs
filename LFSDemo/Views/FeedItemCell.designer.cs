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
	[Register ("FeedItemCell")]
	partial class FeedItemCell
	{
		[Outlet]
		UIKit.NSLayoutConstraint Constraint_ImageViewRatio { get; set; }

		[Outlet]
		UIKit.UIImageView ImageView { get; set; }

		[Outlet]
		UIKit.UILabel MessageLabel { get; set; }

		[Outlet]
		UIKit.UILabel NameLabel { get; set; }

		[Outlet]
		UIKit.UIImageView ProfileImageView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (ImageView != null) {
				ImageView.Dispose ();
				ImageView = null;
			}

			if (MessageLabel != null) {
				MessageLabel.Dispose ();
				MessageLabel = null;
			}

			if (NameLabel != null) {
				NameLabel.Dispose ();
				NameLabel = null;
			}

			if (ProfileImageView != null) {
				ProfileImageView.Dispose ();
				ProfileImageView = null;
			}

			if (Constraint_ImageViewRatio != null) {
				Constraint_ImageViewRatio.Dispose ();
				Constraint_ImageViewRatio = null;
			}
		}
	}
}
