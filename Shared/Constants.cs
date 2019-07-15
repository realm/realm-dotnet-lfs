using System;
using Realms.Sync;

namespace Shared
{
    public static class Constants
    {
        public static readonly Uri AuthUri = new Uri("https://lfsdemo.de1a.cloud.realm.io");
        public const string Username = "user3";
        public const string Password = @"R\9[mKeX1G\ibE_}V'(wdp)*c9z\3\#";

        public static readonly Credentials Credentials = Credentials.UsernamePassword(Username, Password, false);

        public static readonly Uri RealmUri = new Uri("/~/myrealm", UriKind.Relative);
    }
}
