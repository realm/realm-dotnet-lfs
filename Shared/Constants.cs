using Realms.Sync;

namespace Shared
{
    public static class Constants
    {
        private const string Username = "foo@me.com";
        private const string Password = "123456";
        public const string AppId = "lfsdemo-ciacp";

        public static readonly Credentials Credentials = Credentials.EmailPassword(Username, Password);
    }
}
