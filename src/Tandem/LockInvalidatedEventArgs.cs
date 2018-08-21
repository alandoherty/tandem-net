using System;
using System.Collections.Generic;
using System.Text;

namespace Tandem
{
    /// <summary>
    /// Provides event arguments for when a lock expires.
    /// </summary>
    public class LockInvalidatedEventArgs
    {
        #region Properties
        /// <summary>
        /// Gets the reason the lock was invalidated.
        /// </summary>
        public LockInvalidatedReason Reason { get; internal set; }

        /// <summary>
        /// Gets the handle for the lock.
        /// </summary>
        public ILockHandle Handle { get; internal set; }
        #endregion

        /// <summary>
        /// Creates new lock invalidated event arguments.
        /// </summary>
        public LockInvalidatedEventArgs() { }
    }
}
