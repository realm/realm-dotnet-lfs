using Acr.UserDialogs;
using Foundation;
using Realms;
using Realms.Sync;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UIKit;

namespace LFSDemo
{
    public partial class ViewController : UIViewController
    {
        private Realm _realm;

        public ViewController(IntPtr handle) : base(handle)
        {
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            FeedTableView.Source = new FeedDataSource();

            OpenRealm();
        }

        private async Task OpenRealm()
        {
            await Task.Yield();

            var dialog = new ProgressDialog(new ProgressDialogConfig
            {
                IsDeterministic = true,
                Title = "Downloading Realm...",
                AutoShow = true,
                MaskType = MaskType.Black,
            });

            try
            {
                dialog.Show();
                var user = await User.LoginAsync(Constants.Credentials, Constants.AuthUri);
                var config = new FullSyncConfiguration(Constants.RealmUri, user)
                {
                    OnProgress = (progress) =>
                    {
                        dialog.PercentComplete = (int)(progress.TransferredBytes * 100 / progress.TransferableBytes);
                    }
                };

                _realm = await Realm.GetInstanceAsync(config);

                var feedItems = _realm.All<FeedItem>().OrderByDescending(f => f.Date);
                var tvSource = FeedTableView.Source as FeedDataSource;
                tvSource.SetSource(feedItems);
                dialog.Hide();

                FeedTableView.ReloadData();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                dialog.Hide();
                await UserDialogs.Instance.AlertAsync("An error occurred", ex.ToString());
            }
            finally
            {
                await Task.Delay(10);
                dialog.Hide();
            }
        }

        private class FeedDataSource : UITableViewSource
        {
            private FeedItem[] _items = new FeedItem[0];

            public void SetSource(IEnumerable<FeedItem> items)
            {
                _items = items.ToArray();
            }

            public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
            {
                var cell = tableView.DequeueReusableCell("FeedItemCell") as FeedItemCell;
                cell.LoadData(_items[indexPath.Row]);
                return cell;
            }

            public override nint RowsInSection(UITableView tableview, nint section)
            {
                return _items.Length;
            }
        }
    }
}