using Acr.UserDialogs;
using Foundation;
using Newtonsoft.Json;
using Realms;
using Realms.Sync;
using Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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

            this.FeedTableView.Source = new FeedDataSource();

            OpenRealm();
        }

        public override void DidReceiveMemoryWarning()
        {
            base.DidReceiveMemoryWarning();
            // Release any cached data, images, etc that aren't in use.
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
                var user = await Realms.Sync.User.LoginAsync(Constants.Credentials, Constants.AuthUri);
                var config = new FullSyncConfiguration(Constants.RealmUri, user)
                {
                    OnProgress = (progress) =>
                    {
                        dialog.PercentComplete = (int)(progress.TransferredBytes * 100 / progress.TransferableBytes);
                    }
                };

                //Realm.DeleteRealm(config);
                _realm = await Realm.GetInstanceAsync(config);

                var feedItems = _realm.All<FeedItem>().OrderByDescending(f => f.Date);
                var tvSource = this.FeedTableView.Source as FeedDataSource;
                tvSource.SetSource(feedItems);

                this.FeedTableView.ReloadData();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                dialog.Hide();
                await UserDialogs.Instance.AlertAsync("An error occurred", ex.Message);
            }
            finally
            {
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