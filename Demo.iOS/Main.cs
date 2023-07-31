using Demo.iOS;
using Realms.LFS;
using Realms.LFS.Functions;

// This is the main entry point of the application.
// If you want to use a different Application Delegate class from "AppDelegate"
// you can specify it here.

LFSManager.Initialize(new()
{
    RemoteManagerFactory = (config) => new AtlasFunctionsStorageManager(config, "DataFunction")
});

UIApplication.Main(args, null, typeof(AppDelegate));