using System;
using Realms;

namespace Shared
{
    public class User : RealmObject
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public byte[] ProfilePicture { get; set; }

        public string ProfilePictureUrl { get; set; }
    }
}
