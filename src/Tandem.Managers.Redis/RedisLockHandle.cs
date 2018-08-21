﻿using System;
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
        /// Invoked when the lock is invalidated.
        /// </summary>
        public event EventHandler<LockInvalidatedEventArgs> Invalidated;

        internal void OnInvalidated(object sender, LockInvalidatedEventArgs e) {
            // invalidate
            Invalidated?.Invoke(sender, e);

            // release the valid semaphore
            _validSemaphore.Release();
        }

        /// <summary>
        /// Disposes the lock by releasing it.
        /// </summary>
        public void Dispose() {
            Action releaseAction = async () => await _manager.ReleaseAsync(this);
            releaseAction();
        }

        internal RedisLockHandle(RedisLockManager manager) {
            _manager = manager;
        }
    }
}
