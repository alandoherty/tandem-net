using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Tandem
{
    /// <summary>
    /// Defines an interface for a distributed lock manager.
    /// </summary>
    public interface ILockManager
    {
        /// <summary>
        /// Gets or sets the time for locks to expire.
        /// </summary>
        TimeSpan ExpirySpan { get; set; }

        /// <summary>
        /// Locks the specified resource.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <param name="waitTime">The maximum amount of time to wait, not supported by every lock manager.</param>
        /// <exception cref="TimeoutException">The lock could not be obtained within the wait time.</exception>
        /// <returns>The lock handle or null if the lock could not be obtained instantly.</returns>
        Task<ILockHandle> LockAsync(Uri resourceUri, TimeSpan waitTime = default(TimeSpan));

        /// <summary>
        /// Locks the specified resource.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <param name="waitTime">The maximum amount of time to wait, not supported by every lock manager.</param>
        /// <exception cref="TimeoutException">The lock could not be obtained within the wait time.</exception>
        /// <returns>The lock handle or null if the lock could not be obtained instantly.</returns>
        Task<ILockHandle> LockAsync(string resourceUri, TimeSpan waitTime = default(TimeSpan));

        /// <summary>
        /// Gets if the resource is locked.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <returns>If the resource URI is locked.</returns>
        Task<bool> IsLockedAsync(Uri resourceUri);

        /// <summary>
        /// Gets if the resource is locked.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <returns>If the resource URI is locked.</returns>
        Task<bool> IsLockedAsync(string resourceUri);

        /// <summary>
        /// Releases the specified resource lock.
        /// </summary>
        /// <param name="handle">The resource handle.</param>
        /// <returns>If the lock was released.</returns>
        Task<bool> ReleaseAsync(ILockHandle handle);
    }
}
