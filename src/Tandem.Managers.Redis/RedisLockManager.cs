﻿using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
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
        #region Fields
        private ConnectionMultiplexer _connectionMultiplexer = null;
        private IDatabase _database;

        private TimeSpan _expirySpan = TimeSpan.FromSeconds(60);
        private int _disposed = 0;
        private CancellationTokenSource _disposeCancellation = new CancellationTokenSource();

        private List<RedisLockHandle> _handles = new List<RedisLockHandle>();

        private Task _lockExtenderTask = null;
        private string _owner;
        #endregion

        /// <summary>
        /// Gets or sets the default expiry for locks.
        /// </summary>
        public TimeSpan ExpirySpan {
            get {
                return _expirySpan;
            } set {
                _expirySpan = value;
            }
        }

        /// <summary>
        /// Gets if the resource is locked.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <remarks>Be careful when using this operation, the result may be invalidated before this method returns.</remarks>
        /// <returns>If the resource URI is locked.</returns>
        public async Task<LockToken> QueryAsync(Uri resourceUri) {
            if (_disposed == 1)
                throw new ObjectDisposedException("The lock manager has been disposed");

            // query the lock
            RedisValue value = await _database.LockQueryAsync($"tandem.{resourceUri.ToString()}");

            if (value.IsNull)
                return default(LockToken);
            else
                return LockToken.Parse(value);
        }

        /// <summary>
        /// Gets if the resource is locked.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <returns>If the resource URI is locked.</returns>
        public Task<LockToken> QueryAsync(string resourceUri) {
            return QueryAsync(new Uri(resourceUri));
        }

        /// <summary>
        /// Locks the specified resource.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <param name="waitTime">The maximum amount of time to wait.</param>
        /// <returns>The lock handle or null if the lock could not be obtained.</returns>
        public async Task<ILockHandle> LockAsync(Uri resourceUri, TimeSpan waitTime = default(TimeSpan)) {
            if (_disposed == 1)
                throw new ObjectDisposedException("The lock manager has been disposed");

            // generate random token
            LockToken token = new LockToken(Guid.NewGuid(), _owner);

            // get remaining time
            TimeSpan remainingTime = waitTime;

            while (remainingTime >= TimeSpan.Zero) {
                bool gotLock = await _database.LockTakeAsync($"tandem.{resourceUri.ToString()}", token.ToString(), _expirySpan);

                if (gotLock) {
                    // create handle
                    RedisLockHandle handle = new RedisLockHandle(this) {
                        ResourceURI = resourceUri,
                        Token = token
                    };

                    // add handle
                    lock(_handles) {
                        _handles.Add(handle);
                    }

                    return handle;
                } else {
                    if (waitTime == TimeSpan.Zero)
                        return null;

                    if (remainingTime > TimeSpan.FromMilliseconds(3000)) {
                        // no point waiting anymore, no cigar for the lock
                        return null;
                    } else {
                        // wait 3 seconds until we try again
                        await Task.Delay(3000);
                        remainingTime -= TimeSpan.FromSeconds(3);
                    }
                }
            }

            return null;
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

        /// <summary>
        /// Releases a lock.
        /// </summary>
        /// <param name="handle">The handle.</param>
        /// <returns>If the lock was released by this manager.</returns>
        public async Task<bool> ReleaseAsync(ILockHandle handle) {
            if (_disposed == 1)
                throw new ObjectDisposedException("The lock manager has been disposed");

            if (handle.Manager != this)
                throw new InvalidOperationException("The handle does not belong this lock manager");

            // release the lock with the token.
            bool success = await _database.LockReleaseAsync($"tandem.{handle.ResourceURI.ToString()}", ((RedisLockHandle)handle).Token.ToString());

            lock (_handles) {
                try {
                    _handles.Remove((RedisLockHandle)handle);
                } catch (Exception) { }
            }

            return success;
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

        /// <summary>
        /// The internal lock extender.
        /// </summary>
        /// <returns></returns>
        private async Task LockExtender() {
            while(!_disposeCancellation.IsCancellationRequested) {
                // wait
                await Task.Delay(TimeSpan.FromSeconds(5));

                // get expiry time
                TimeSpan expiryTime = _expirySpan;

                // gather all locks that need to be refreshed or expired
                RedisLockHandle[] refreshableLocks = null;
                IEnumerable<RedisLockHandle> expiredLocks = null;

                lock(_handles) {
                    refreshableLocks = _handles.Where(h => h.RefreshedAt < (DateTime.UtcNow - TimeSpan.FromSeconds(30)) || h.ExpiresAt > DateTime.UtcNow).ToArray();
                    expiredLocks = _handles.Where(h => h.ExpiresAt < DateTime.UtcNow);

                    // process any expires locks
                    foreach (RedisLockHandle handle in expiredLocks) {
                        _handles.Remove(handle);
                    }
                }

                // build refresh lock tasks
                List<Task<bool>> refreshLockTasks = new List<Task<bool>>(refreshableLocks.Length);
                Dictionary<Task<bool>, RedisLockHandle> refreshLockHandles = new Dictionary<Task<bool>, RedisLockHandle>();
                Dictionary<Task<bool>, DateTime> refreshLockTimes = new Dictionary<Task<bool>, DateTime>();

                foreach (RedisLockHandle handle in refreshableLocks) {
                    // create task
                    Task<bool> task = _database.LockExtendAsync($"tandem.{handle.ResourceURI.ToString()}", handle.Token.ToString(), expiryTime);

                    // add and map
                    refreshLockTasks.Add(task);
                    refreshLockHandles[task] = handle;
                    refreshLockTimes[task] = DateTime.UtcNow;
                }

                // refresh all locks
                try {
                    await Task.WhenAll(refreshLockTasks);
                } catch (Exception) { }

                // map any sucesses
                List<RedisLockHandle> refreshFailures = new List<RedisLockHandle>();

                foreach(var task in refreshLockTasks) {
                    // find associated handle
                    RedisLockHandle handle = refreshLockHandles[task];
                    DateTime time = refreshLockTimes[task];

                    if (task.Result == true) {
                        handle.RefreshedAt = time;
                        handle.ExpiresAt = time + expiryTime;
                    } else {
                        refreshFailures.Add(handle);
                    }
                }

                lock(_handles) {
                    foreach(var handle in refreshFailures) {
                        try {
                            _handles.Remove(handle);
                        } catch (Exception) { }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new redis lock manager.
        /// </summary>
        /// <param name="connectionMultiplexer">The connection multiplexer.</param>
        /// <param name="owner">The owner identifier to use in lock tokens.</param>
        public RedisLockManager(ConnectionMultiplexer connectionMultiplexer, string owner) {
            // setup redis connections
            _connectionMultiplexer = connectionMultiplexer;
            _database = _connectionMultiplexer.GetDatabase();

            // start lock extender
            _lockExtenderTask = LockExtender();

            _owner = owner;
        }

        /// <summary>
        /// Creates a new redis lock manager.
        /// </summary>
        /// <param name="connectionMultiplexer">The connection multiplexer.</param>
        public RedisLockManager(ConnectionMultiplexer connectionMultiplexer)
            : this (connectionMultiplexer, null) {
        }
    }
}
