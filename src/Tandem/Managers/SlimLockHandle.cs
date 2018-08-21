using System;
using System.Collections.Generic;
using System.Text;

namespace Tandem.Managers
{
    /// <summary>
    /// Represents a slim lock handle.
    /// </summary>
    public class SlimLockHandle : ILockHandle, IDisposable
    {
        private SlimLockManager _manager = null;

        #region Properties
        /// <summary>
        /// Gets when the slim lock handle expires (never).
        /// </summary>
        public DateTime ExpiresAt { get; } = DateTime.MaxValue;

        /// <summary>
        /// Gets when the slim lock handle was last refreshed (not required).
        /// </summary>
        public DateTime RefreshedAt { get; } = DateTime.UtcNow;

        /// <summary>
        /// Gets if this handle is valid.
        /// </summary>
        public bool IsValid { get; internal set; }
        
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
        /// Gets the lock token.
        /// </summary>
        public LockToken Token { get; internal set; }
        #endregion

        /// <summary>
        /// Invoked when the lock is invalidated.
        /// </summary>
        public event EventHandler<LockInvalidatedEventArgs> Invalidated;

        #region Methods
        internal void OnInvalidated(object sender, LockInvalidatedEventArgs e) {
            Invalidated?.Invoke(sender, e);
            IsValid = false;
        }

        /// <summary>
        /// Dispose and release the underlying handle.
        /// </summary>
        public void Dispose() {
            _manager.ReleaseAsync(this).Wait();
        }
        #endregion

        #region Constructors
        internal SlimLockHandle(SlimLockManager manager) {
            IsValid = true;
            _manager = manager;
        }
        #endregion
    }
}
