using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Tandem.Managers
{
    /// <summary>
    /// Represents a redis lock handle.
    /// </summary>
    public class RedisLockHandle : ILockHandle
    {
        private ILockManager _manager;
        private SemaphoreSlim _validSemaphore = new SemaphoreSlim(0, 1);

        public DateTime ExpiresAt { get; } = DateTime.MaxValue;

        public DateTime RefreshedAt { get; } = DateTime.UtcNow;

        public bool IsValid { get; internal set; }

        /// <summary>
        /// Gets the lock token.
        /// </summary>
        public string Token { get; internal set; }

        public ILockManager Manager {
            get {
                return _manager;
            }
        }

        /// <summary>
        /// Gets the resource URI.
        /// </summary>
        public Uri ResourceURI { get; internal set; }

        public event EventHandler<LockInvalidatedEventArgs> Invalidated;

        internal void OnInvalidated(object sender, LockInvalidatedEventArgs e) {
            // invalidate
            Invalidated?.Invoke(sender, e);
            IsValid = false;

            // release the valid semaphore
            _validSemaphore.Release();
        }

        public void Dispose() {
            Action releaseAction = async () => await _manager.ReleaseAsync(this);
            releaseAction();
        }

        internal RedisLockHandle(RedisLockManager manager) {
            _manager = manager;
        }
    }
}
