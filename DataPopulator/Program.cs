using Amazon;
using Amazon.Runtime;
using Bogus;
using Nito.AsyncEx;
using Realms;
using Realms.LFS;
using Realms.LFS.S3;
using Realms.Sync;
using Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DataPopulator
{
    internal class Program
    {
        private const int UsersCount = 200;
        private const int FeedItemsCount = 10 * UsersCount;

        private static App _app = null!;

        private static void Main(string[] args)
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "Realms");
            Directory.CreateDirectory(basePath);
            _app = App.Create(new AppConfiguration(Constants.AppId)
            {
                BaseFilePath = basePath,
            });

            var credentials = new BasicAWSCredentials(Constants.AwsAccessKey, Constants.AwsSecretKey);
            FileManager.Initialize(new FileManagerOptions
            {
                PersistenceLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                RemoteManagerFactory = (config) => new S3FileManager(config, credentials, RegionEndpoint.EUNorth1)
            });

            AsyncContext.Run(MainAsync);
        }

        private static async Task MainAsync()
        {
            var faker = new Faker();

            var startDate = DateTime.Parse("2018-01-01 00:00");
            var endDate = DateTime.Parse("2020-01-01 00:00");
            var bogusUser = new Faker<FeedUser>()
                .RuleFor(u => u.FirstName, f => f.Name.FirstName())
                .RuleFor(u => u.LastName, f => f.Name.LastName());

            var bogusFeedItem = new Faker<FeedItem>()
                .RuleFor(i => i.Title, f => f.Lorem.Sentence(f.Random.Int(2, 5)))
                .RuleFor(i => i.Message, f => f.Lorem.Sentence(f.Random.Int(4, 15)))
                .RuleFor(i => i.Date, f => f.Date.Between(startDate, endDate));

            var user = await _app.LogInAsync(Constants.Credentials);
            var config = new FlexibleSyncConfiguration(user)
            {
                PopulateInitialSubscriptions = (r) =>
                {
                    r.Subscriptions.Add(r.All<FeedItem>());
                    r.Subscriptions.Add(r.All<FeedUser>());
                }
            };
            using var realm = await Realm.GetInstanceAsync(config);
            using var client = new HttpClient();
            var users = bogusUser.Generate(UsersCount);
            await ExecuteInParallel(users, async u =>
            {
                try
                {
                    var pictureUrl = $"https://i.pravatar.cc/1000?u={u.Id}";

                    var bytes = await client.GetByteArrayAsync(pictureUrl);
                    u.ProfilePictureData = new FileData(new MemoryStream(bytes), pictureUrl);
                }
                catch
                {
                }
                realm.Write(() => realm.Add(u));
            }, 50);

            var feedItems = bogusFeedItem.Generate(FeedItemsCount);
            await ExecuteInParallel(feedItems, async i =>
            {
                if (faker.Random.Bool(0.6f))
                {
                    try
                    {
                        var imageUrl = faker.Image.PicsumUrl(1000, 370);
                        var bytes = await client.GetByteArrayAsync(imageUrl);
                        i.ImageData = new FileData(new MemoryStream(bytes), imageUrl);

                        //i.Image = bytes;
                    }
                    catch
                    {
                    }
                }

                i.Author = faker.PickRandom(users);
                realm.Write(() =>
                {
                    realm.Add(i);
                });
            }, 50);
        }

        private static async Task ExecuteInParallel<T>(IEnumerable<T> collection,
                                           Func<T, Task> processor,
                                           int degreeOfParallelism)
        {
            ConcurrentQueue<T> queue = new(collection);
            var tasks = Enumerable.Range(0, degreeOfParallelism).Select(async _ =>
            {
                while (queue.TryDequeue(out var item))
                {
                    await processor(item);
                }
            });

            await Task.WhenAll(tasks);
        }
    }
}
