using System;
using System.Collections.Generic;
using System.Text;

namespace Tandem.Managers
{
    public class SlimLockHandle : ILockHandle, IDisposable
    {
        private SlimLockManager _manager = null;

        public DateTime ExpiresAt { get; } = DateTime.MaxValue;

        public DateTime RefreshedAt { get; } = DateTime.UtcNow;

        public bool IsValid { get; internal set; }

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
            Invalidated?.Invoke(sender, e);
            IsValid = false;
        }

        public void Dispose() {
            _manager.ReleaseAsync(this).Wait();
        }

        internal SlimLockHandle(SlimLockManager manager) {
            IsValid = true;
            _manager = manager;
        }
    }
}
