using System;
using System.Collections.Generic;
using System.Text;

namespace Tandem.Managers
{
    /// <summary>
    /// Provides event arguments for when a lock invalidated by other means.
    /// </summary>
    public class RedisLockInvalidatedEventArgs
    {
        #region Properties
        /// <summary>
        /// Gets the handle for the lock.
        /// </summary>
        public ILockHandle Handle { get; internal set; }
        #endregion

        /// <summary>
        /// Creates new lock invalidated event arguments.
        /// </summary>
        public RedisLockInvalidatedEventArgs() { }
    }
}
