using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tandem.Managers
{
    /// <summary>
    /// Provides an in-process locking manager.
    /// </summary>
    public sealed class SlimLockManager : ILockManager
    {
        #region Fields
        private List<SlimLockHandle> _handles = new List<SlimLockHandle>();
        private Dictionary<string, List<TaskCompletionSource<ILockHandle>>> _lockQueues = new Dictionary<string, List<TaskCompletionSource<ILockHandle>>>();
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the default expiry for locks.
        /// </summary>
        public TimeSpan ExpirySpan {
            get {
                return Timeout.InfiniteTimeSpan;
            }
            set {
                throw new InvalidOperationException("The expiry time for process locks is infinite");
            }
        }

        /// <summary>
        /// Gets or sets the owner.
        /// </summary>
        public string Owner {
            get {
                return string.Empty;
            } set {
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Locks a resource.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <param name="waitTime">The wait time.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public async Task<ILockHandle> LockAsync(Uri resourceUri, TimeSpan waitTime = default(TimeSpan), CancellationToken cancellationToken = default(CancellationToken)) {
            if (!resourceUri.Scheme.Equals("tandem", StringComparison.CurrentCultureIgnoreCase))
                throw new FormatException("The protocol scheme must be tandem");

            lock (_handles) {
                SlimLockHandle handle = _handles.SingleOrDefault(h => h.ResourceURI.ToString().Equals(resourceUri.ToString(), StringComparison.CurrentCultureIgnoreCase));

                if (handle != null) {
                    // check if we don't want to wait
                    if (waitTime == TimeSpan.Zero)
                        return null;

                    // fall through
                } else {
                    handle = new SlimLockHandle(this) {
                        ResourceURI = resourceUri,
                        Token = new LockToken(Guid.NewGuid(), null)
                    };

                    _handles.Add(handle);
                    return handle;
                }
            }

            // add to lock queue
            TaskCompletionSource<ILockHandle> handleTaskSource = new TaskCompletionSource<ILockHandle>();

            lock (_lockQueues) {
                // get waiting list or create
                List<TaskCompletionSource<ILockHandle>> waitingList = null;

                if (!_lockQueues.TryGetValue(resourceUri.ToString(), out waitingList)) {
                    waitingList = new List<TaskCompletionSource<ILockHandle>>();
                    _lockQueues[resourceUri.ToString()] = waitingList;
                }

                // add task
                waitingList.Add(handleTaskSource);
            }

            // race between the lock and the delay, and optionally a cancellation signal
            bool timedOut = false, cancelled = false;

            try {
                Task waitDelay = Task.Delay(waitTime, cancellationToken);
                timedOut = await Task.WhenAny(waitDelay, handleTaskSource.Task).ConfigureAwait(false) == waitDelay;
            } catch (OperationCanceledException) {
                cancelled = true;
            }

            // race between the delay or the lock being released to us
            if (timedOut || cancelled) {
                lock (_lockQueues) {
                    // get waiting list or create
                    List<TaskCompletionSource<ILockHandle>> waitingList = null;

                    if (!_lockQueues.TryGetValue(resourceUri.ToString(), out waitingList)) {
                        return handleTaskSource.Task.Result;
                    }

                    // check if we've completed already
                    if (!waitingList.Contains(handleTaskSource))
                        return handleTaskSource.Task.Result;
                    else {
                        // remove from waiting list now
                        waitingList.Remove(handleTaskSource);

                        if (waitingList.Count == 0)
                            _lockQueues.Remove(resourceUri.ToString());

                        return null;
                    }
                }
            } else {
                // we got the lock and wern't cancelled or timed out
                return handleTaskSource.Task.Result;
            }
        }

        /// <summary>
        /// Releases a lock.
        /// </summary>
        /// <param name="handle">The handle.</param>
        /// <returns></returns>
        public Task<bool> ReleaseAsync(ILockHandle handle) {
            bool removed = false;

            // try and remove the handle from our lokc list
            lock(_handles) {
                if (_handles.Contains(handle)) {
                    _handles.Remove((SlimLockHandle)handle);

                    // we suceeded
                    removed = true;
                }
            }

            // check if any locks waiting for this lock to be removed
            if (removed) {
                lock(_lockQueues) {
                    while (true) {
                        // get waiting list or create
                        List<TaskCompletionSource<ILockHandle>> waitingList = null;

                        // check if we did actually complete
                        if (_lockQueues.TryGetValue(handle.ResourceURI.ToString(), out waitingList)) {
                            // get the next lock to fulfill
                            TaskCompletionSource<ILockHandle> waitingTask = waitingList[0];
                            waitingList.RemoveAt(0);

                            // create our handle
                            SlimLockHandle newHandle = new SlimLockHandle(this) {
                                ResourceURI = handle.ResourceURI,
                                Token = new LockToken(Guid.NewGuid(), null)
                            };

                            // add to lock list
                            _handles.Add(newHandle);

                            // try and signal that the lock has been granted
                            bool signaledLock = waitingTask.TrySetResult(newHandle);

                            // remove if waiting list is empty
                            if (waitingList.Count == 0) {
                                _lockQueues.Remove(handle.ResourceURI.ToString());
                            }

                            // if we signalled a lock or we're out of locks stop
                            if (signaledLock || waitingList.Count == 0)
                                break;
                        }
                    }
                }

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        /// <summary>
        /// Locks the specified resource.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <param name="waitTime">The maximum amount of time to wait.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The lock handle or null if the lock could not be obtained.</returns>
        public Task<ILockHandle> LockAsync(string resourceUri, TimeSpan waitTime = default(TimeSpan), CancellationToken cancellationToken = default(CancellationToken)) {
            return LockAsync(new Uri(resourceUri), waitTime);
        }

        /// <summary>
        /// Gets if the resource is locked.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <returns>If the resource URI is locked.</returns>
        public Task<LockToken> QueryAsync(Uri resourceUri) {
            if (!resourceUri.Scheme.Equals("tandem", StringComparison.CurrentCultureIgnoreCase))
                throw new FormatException("The protocol scheme must be tandem");

            lock (_handles) {
                SlimLockHandle handle =  _handles.SingleOrDefault(h => h.ResourceURI.ToString().Equals(resourceUri.ToString(), StringComparison.CurrentCultureIgnoreCase));

                if (handle == null)
                    return Task.FromResult(default(LockToken));
                else
                    return Task.FromResult(handle.Token);
            }
        }

        /// <summary>
        /// Gets if the resource is locked.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <returns>If the resource URI is locked.</returns>
        public Task<LockToken> QueryAsync(string resourceUri) {
            return QueryAsync(new Uri(resourceUri));
        }
        #endregion

        /// <summary>
        /// Creates an in-process locking manager.
        /// </summary>
        public SlimLockManager() {
        }
    }
}
