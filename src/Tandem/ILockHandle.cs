using System;
using System.Collections.Generic;
using System.Text;

namespace Tandem
{
    /// <summary>
    /// Defines an interface for a lock handle.
    /// </summary>
    public interface ILockHandle : IDisposable
    {
        /// <summary>
        /// Gets the manager for this handle.
        /// </summary>
        ILockManager Manager { get; }

        /// <summary>
        /// Gets the date the lock expires at.
        /// </summary>
        DateTime ExpiresAt { get; }

        /// <summary>
        /// Gets the date when the lock was last refreshed.
        /// </summary>
        DateTime RefreshedAt { get; }

        /// <summary>
        /// Gets if the lock is still valid.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Gets the resource URI.
        /// </summary>
        Uri ResourceURI { get; }

        /// <summary>
        /// Gets the lock token.
        /// </summary>
        LockToken Token { get; }
    }
}
