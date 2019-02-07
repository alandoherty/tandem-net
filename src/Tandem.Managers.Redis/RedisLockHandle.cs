using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Tandem.Managers
{
    /// <summary>
    /// Represents a redis lock handle.
    /// </summary>
    public sealed class RedisLockHandle : ILockHandle
    {
        private ILockManager _manager;
        private SemaphoreSlim _validSemaphore = new SemaphoreSlim(0, 1);

        /// <summary>
        /// Gets when this lock expires.
        /// </summary>
        public DateTime ExpiresAt { get; internal set; } = DateTime.MaxValue;

        /// <summary>
        /// Gets when the lock was last refreshed.
        /// </summary>
        public DateTime RefreshedAt { get; internal set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets if the lock handle is still valid.
        /// </summary>
        public bool IsValid {
            get {
                return ExpiresAt < DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Gets the lock token.
        /// </summary>
        public LockToken Token { get; internal set; }

        /// <summary>
        /// Gets the lock manager for this handle.
        /// </summary>
        public ILockManager Manager {
            get {
                return _manager;
            }
        }

        /// <summary>
        /// Gets the resource URI.
        /// </summary>
        public Uri ResourceURI { get; internal set; }

        /// <summary>
        /// Disposes the lock by releasing it.
        /// </summary>
        /// <remarks>It is important to note that this operation does not block, meaning you cannot guarentee that the lock has been fully released when Dispose returns. Use <see cref="RedisLockManager.ReleaseAsync(ILockHandle)"/> instead.</remarks>
        public void Dispose() {
            _manager.ReleaseAsync(this).ConfigureAwait(false);
        }

        internal RedisLockHandle(RedisLockManager manager) {
            _manager = manager;
        }
    }
}
