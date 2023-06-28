using SDWebImage;
using Shared;
using Realms.LFS;

namespace Demo.iOS
{
    public partial class FeedItemCell : UITableViewCell
    {
        public FeedItemCell(ObjCRuntime.NativeHandle handle) : base(handle)
        {
        }

        public void LoadData(FeedItem item)
        {
            if (item.Author != null)
            {
                NameLabel.Text = item.Author.FirstName + " " + item.Author.LastName;
                SetImage(ProfileImageView, item.Author.ProfilePictureData);
            }
            MessageLabel.Text = item.Message;
            SetImage(ImageView, item.ImageData);
            if (SetImage(ImageView, item.ImageData))
            {
                Constraint_ImageViewRatio.Constant = 0;
            }
            else
            {
                Constraint_ImageViewRatio.Constant = ImageView.Frame.Width;
            }

            NeedsUpdateConstraints();
            SetNeedsLayout();
        }

        private static bool SetImage(UIImageView imageView, FileData? image)
        {
            if (image != null)
            {
                if (image.Url != null)
                {
                    imageView.Sd_setImageWithURL(NSUrl.FromString(image.Url));
                    return true;
                }
                
                // if (File.Exists(image.LocalUrl))
                // {
                //     UIImage.LoadFromData(NSData.FromFile(image.LocalUrl));
                //     return true;
                // }
            }

            imageView.Image = null;
            return false;
        }
    }
}
