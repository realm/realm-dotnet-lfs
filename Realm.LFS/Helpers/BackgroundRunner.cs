using Nito.AsyncEx;
using System;
using System.Threading.Tasks;

namespace Realms.LFS
{
    internal class BackgroundRunner
    {
        private readonly AsyncContextThread _backgroundThread = new AsyncContextThread();
        private readonly RealmConfigurationBase _config;

        private Realm _realm;
        private Realm Realm
        {
            get
            {
                if (_realm == null)
                {
                    _realm = Realms.Realm.GetInstance(_config);
                }

                return _realm;
            }
        }

        public BackgroundRunner(RealmConfigurationBase config)
        {
            _config = config;
        }

        public Task Execute(Action<Realm> action)
        {
            return _backgroundThread.Factory.Run(() => action(Realm));
        }

        public Task<T> Execute<T>(Func<Realm, T> func)
        {
            return _backgroundThread.Factory.Run(() => func(Realm));
        }

        public Task Execute(Func<Realm, Task> taskAction)
        {
            return _backgroundThread.Factory.Run(() => taskAction(Realm));
        }

        public Task<T> Execute<T>(Func<Realm, Task<T>> taskFunc)
        {
            return _backgroundThread.Factory.Run(() => taskFunc(Realm));
        }
    }
}
