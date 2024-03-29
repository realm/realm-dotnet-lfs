﻿using Acr.UserDialogs;
using Realms;
using Realms.Sync;
using Shared;

namespace Demo.iOS
{
    public partial class ViewController : UIViewController
    {
        private Realm _realm = null!;

        public ViewController(IntPtr handle) : base(handle)
        {
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            FeedTableView.Source = new FeedDataSource();

            _ = OpenRealm();
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
                var app = App.Create(Constants.AppId);
                var user = await app.LogInAsync(Constants.Credentials);
                var config = new FlexibleSyncConfiguration(user)
                {
                    PopulateInitialSubscriptions = (r) =>
                    {
                        r.Subscriptions.Add(r.All<FeedItem>());
                        r.Subscriptions.Add(r.All<FeedUser>());
                    }
                };

                _realm = await Realm.GetInstanceAsync(config);

                var feedItems = _realm.All<FeedItem>().OrderByDescending(f => f.Date);
                var tvSource = FeedTableView.Source as FeedDataSource;
                tvSource!.SetSource(feedItems);
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
            private FeedItem[] _items = Array.Empty<FeedItem>();

            public void SetSource(IEnumerable<FeedItem> items)
            {
                _items = items.ToArray();
            }

            public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
            {
                var cell = tableView.DequeueReusableCell("FeedItemCell") as FeedItemCell;
                cell!.LoadData(_items[indexPath.Row]);
                return cell;
            }

            public override nint RowsInSection(UITableView tableview, nint section)
            {
                return _items.Length;
            }
        }
    }
}