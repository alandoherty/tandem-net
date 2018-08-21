using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tandem.Managers
{
    /// <summary>
    /// Provides functionality for locking over redis.
    /// </summary>
    public class RedisLockManager : ILockManager, IDisposable
    {
        private ConnectionMultiplexer _connectionMultiplexer = null;
        private IDatabase _database;
        private TimeSpan _expirySpan = TimeSpan.FromSeconds(60);
        private int _disposed = 0;
        private CancellationTokenSource _disposeCancellation = new CancellationTokenSource();
        private List<RedisLockHandle> _handles = new List<RedisLockHandle>();
        private Task _lockExtenderTask = null;

        public TimeSpan ExpirySpan {
            get {
                return _expirySpan;
            } set {
                _expirySpan = value;
            }
        }

        public async Task<bool> IsLockedAsync(Uri resourceUri) {
            if (_disposed == 1)
                throw new ObjectDisposedException("The lock manager has been disposed");

            // query the lock
            RedisValue value = await _database.LockQueryAsync($"tandem.{resourceUri.ToString()}");

            return !value.IsNull;
        }

        public Task<bool> IsLockedAsync(string resourceUri) {
            return IsLockedAsync(new Uri(resourceUri));
        }

        public async Task<ILockHandle> LockAsync(Uri resourceUri, TimeSpan waitTime = default(TimeSpan)) {
            if (_disposed == 1)
                throw new ObjectDisposedException("The lock manager has been disposed");

            // generate random token
            string token = Guid.NewGuid().ToString();

            // take lock
            bool gotLock = await _database.LockTakeAsync($"tandem.{resourceUri.ToString()}", token, _expirySpan);

            if (gotLock) {
                return new RedisLockHandle(this) {
                    ResourceURI = resourceUri,
                    IsValid = true,
                    Token = token
                };
            } else {
                return null;
            }
        }

        /// <summary>
        /// Locks the specified resource.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <param name="waitTime">The maximum amount of time to wait.</param>
        /// <returns>The lock handle or null if the lock could not be obtained.</returns>
        public Task<ILockHandle> LockAsync(string resourceUri, TimeSpan waitTime = default(TimeSpan)) {
            return LockAsync(new Uri(resourceUri), waitTime);
        }

        public Task<bool> ReleaseAsync(ILockHandle handle) {
            if (_disposed == 1)
                throw new ObjectDisposedException("The lock manager has been disposed");

            if (handle.Manager != this)
                throw new InvalidOperationException("The handle does not belong this lock manager");

            // release the lock with the token.
            return _database.LockReleaseAsync($"tandem.{handle.ResourceURI.ToString()}", ((RedisLockHandle)handle).Token);
        }

        /// <summary>
        /// Disposes the redis lock manager.
        /// </summary>
        public void Dispose() {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
                return;

            // cancel
            _disposeCancellation.Cancel();
        }

        public async Task LockExtender() {
            while(!_disposeCancellation.IsCancellationRequested) {
                // wait
                await Task.Delay(TimeSpan.FromSeconds(30));

                // gather all locks that need to be refreshed
                RedisLockHandle[] locks = null;

                lock(_handles) {
                    locks = _handles.Where(h => h.RefreshedAt < (DateTime.UtcNow - TimeSpan.FromSeconds(30))).ToArray();
                }

                foreach(RedisLockHandle handle in locks) {
                    // refresh lock
                    Console.WriteLine("Refresh lock: " + handle.Token);
                }
            }
        }

        public RedisLockManager(ConnectionMultiplexer connectionMultiplexer) {
            // setup redis connections
            _connectionMultiplexer = connectionMultiplexer;
            _database = _connectionMultiplexer.GetDatabase();

            _lockExtenderTask = LockExtender();
        }
    }
}
