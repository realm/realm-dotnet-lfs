﻿using Realms.Sync;

namespace Shared
{
    public static class Constants
    {
        public const string Username = "my-user";
        public const string Password = "pass";
        public const string AppId = "<fill-me>";

        public const string AwsAccessKey = "<fill-me>";
        public const string AwsSecretKey = "<fill-me>";

        public static readonly Credentials Credentials = Credentials.EmailPassword(Username, Password);
    }
}
