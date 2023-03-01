using Realms.Sync;

namespace Shared
{
    public static class Constants
    {
        public const string Username = "my-user";
        public const string Password = "pass";

        public static readonly Credentials Credentials = Credentials.EmailPassword(Username, Password);
    }
}
