using Foundation;
using SDWebImage;
using Shared;
using System;
using UIKit;

namespace LFSDemo
{
    public partial class FeedItemCell : UITableViewCell
    {
        public FeedItemCell(IntPtr handle) : base(handle)
        {
        }

        public void LoadData(FeedItem item)
        {
            NameLabel.Text = item.Author.FirstName + " " + item.Author.LastName;
            MessageLabel.Text = item.Message;
            if (item.Author?.ProfilePictureData?.Url != null)
            {
                ProfileImageView.SetImage(NSUrl.FromString(item.Author?.ProfilePictureData?.Url));
            }
            else
            {
                ProfileImageView.Image = UIImage.LoadFromData(NSData.FromArray(item.Author.ProfilePicture));
            }

            if (item.ImageData?.Url != null)
            {
                ImageView.SetImage(NSUrl.FromString(item.ImageData.Url));
                Constraint_ImageViewRatio.Constant = 0;
            }
            else if (item.Image != null)
            {
                ImageView.Image = UIImage.LoadFromData(NSData.FromArray(item.Image));
                Constraint_ImageViewRatio.Constant = 0;
            }
            else
            {
                ImageView.Image = null;
                Constraint_ImageViewRatio.Constant = ImageView.Frame.Width;
            }

            NeedsUpdateConstraints();
            SetNeedsLayout();
        }
    }
}
