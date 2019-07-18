using System;
using Realms;
using Realms.LFS;

namespace Shared
{
    public class FeedItem : RealmObject
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Title { get; set; }

        public string Message { get; set; }

        public DateTimeOffset Date { get; set; }

        public FeedUser Author { get; set; }

        public byte[] Image { get; set; }

        public FileData ImageData { get; set; }
    }
}
