using Demo.iOS;
using Realms.LFS;

// This is the main entry point of the application.
// If you want to use a different Application Delegate class from "AppDelegate"
// you can specify it here.

FileManager.Initialize(new()
{
    RemoteManagerFactory = (config) => new FunctionsFileManager(config, "DataFunction")
});

UIApplication.Main(args, null, typeof(AppDelegate));