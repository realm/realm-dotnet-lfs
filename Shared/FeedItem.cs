using System;
using Realms;

namespace Shared
{
    public class FeedItem : RealmObject
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Title { get; set; }

        public string Message { get; set; }

        public DateTimeOffset Date { get; set; }

        public User Author { get; set; }

        public byte[] Image { get; set; }

        public string ImageUrl { get; set; }
    }
}
