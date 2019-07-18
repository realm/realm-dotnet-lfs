using System;
using Realms.Sync;

namespace Shared
{
    public static class Constants
    {
        public static readonly Uri AuthUri = new Uri("https://lfsdemo.de1a.cloud.realm.io");
        public const string Username = "my-user";
        public const string Password = "pass";

        public static readonly Credentials Credentials = Credentials.UsernamePassword(Username, Password);

        public static readonly Uri RealmUri = new Uri("/~/myrealm", UriKind.Relative);
    }
}
