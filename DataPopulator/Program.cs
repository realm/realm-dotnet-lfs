using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bogus;
using Nito.AsyncEx;
using Realms;
using Realms.Sync;
using Shared;

using User = Shared.User;

namespace DataPopulator
{
    class Program
    {
        private const int UsersCount = 200;
        private const int FeedItemsCount = 10 * UsersCount;

        static void Main(string[] args)
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "Realms");
            Directory.CreateDirectory(basePath);
            FullSyncConfiguration.Initialize(UserPersistenceMode.Disabled, basePath: basePath);

            AsyncContext.Run(MainAsync);
        }

        static async Task MainAsync()
        {
            var faker = new Faker();

            var startDate = DateTime.Parse("2018-01-01 00:00");
            var endDate = DateTime.Parse("2020-01-01 00:00");
            var bogusUser = new Faker<User>()
                .RuleFor(u => u.FirstName, f => f.Name.FirstName())
                .RuleFor(u => u.LastName, f => f.Name.LastName());

            var bogusFeedItem = new Faker<FeedItem>()
                .RuleFor(i => i.Title, f => f.Lorem.Sentence(f.Random.Int(2, 5)))
                .RuleFor(i => i.Message, f => f.Lorem.Sentence(f.Random.Int(4, 15)))
                .RuleFor(i => i.Date, f => f.Date.Between(startDate, endDate));

            var user = await Realms.Sync.User.LoginAsync(Constants.Credentials, Constants.AuthUri);
            var config = new FullSyncConfiguration(Constants.RealmUri, user);
            using (var realm = await Realm.GetInstanceAsync(config))
            using (var client = new HttpClient())
            {
                var users = bogusUser.Generate(UsersCount);
                await ExecuteInParallel(users, async u =>
                {
                    try
                    {
                        u.ProfilePictureUrl = $"https://i.pravatar.cc/1000?u={u.Id}";
                        //u.ProfilePicture = await client.GetByteArrayAsync(u.ProfilePictureUrl);
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
                            i.ImageUrl = faker.Image.PicsumUrl(1000, 370);
                            //i.Image = await client.GetByteArrayAsync(i.ImageUrl);
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
        }

        private static async Task ExecuteInParallel<T>(IEnumerable<T> collection,
                                           Func<T, Task> processor,
                                           int degreeOfParallelism)
        {
            var queue = new ConcurrentQueue<T>(collection);
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
