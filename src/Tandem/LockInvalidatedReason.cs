using System;
using System.Collections.Generic;
using System.Text;

namespace Tandem
{
    /// <summary>
    /// Defines a reason why a lock was invalidated.
    /// </summary>
    public enum LockInvalidatedReason
    {
        /// <summary>
        /// The lock has expired.
        /// </summary>
        Expired,

        /// <summary>
        /// The lock was lost due a transient loss.
        /// </summary>
        Transient,

        /// <summary>
        /// The lock was successfully released.
        /// </summary>
        Released,

        /// <summary>
        /// The lock was invalidated for an unknown reason.
        /// </summary>
        Other
    }
}
