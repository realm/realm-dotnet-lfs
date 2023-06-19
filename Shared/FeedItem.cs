using System;
using Realms;
using Realms.LFS;

namespace Shared
{
    public partial class FeedItem : IRealmObject
    {
        [PrimaryKey, MapTo("_id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Title { get; set; }

        public string Message { get; set; }

        public DateTimeOffset Date { get; set; }

        public FeedUser? Author { get; set; }

        public FileData? ImageData { get; set; }
    }
}
