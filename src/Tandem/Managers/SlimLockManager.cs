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
    public class SlimLockManager : ILockManager
    {
        private List<SlimLockHandle> _handles = new List<SlimLockHandle>();
        private Dictionary<string, List<TaskCompletionSource<ILockHandle>>> _lockQueues = new Dictionary<string, List<TaskCompletionSource<ILockHandle>>>();

        public TimeSpan ExpirySpan {
            get {
                return Timeout.InfiniteTimeSpan;
            }
            set {
                throw new InvalidOperationException("The expiry time for process locks is infinite");
            }
        }

        /// <summary>
        /// Locks a resource.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <param name="waitTime">The wait time.</param>
        /// <returns></returns>
        public async Task<ILockHandle> LockAsync(Uri resourceUri, TimeSpan waitTime = default(TimeSpan)) {
            lock (_handles) {
                SlimLockHandle handle = _handles.SingleOrDefault(h => h.ResourceURI.ToString().Equals(resourceUri.ToString(), StringComparison.CurrentCultureIgnoreCase));

                if (handle != null) {
                    // check if we don't want to wait
                    if (waitTime == TimeSpan.Zero)
                        return null;

                    // fall through
                } else {
                    handle = new SlimLockHandle(this) {
                        ResourceURI = resourceUri
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

            // wait
            Task waitDelay = Task.Delay(waitTime);

            // race between the delay or the lock being released to us
            if (await Task.WhenAny(waitDelay, handleTaskSource.Task) == waitDelay) {
                lock (_lockQueues) {
                    // get waiting list or create
                    List<TaskCompletionSource<ILockHandle>> waitingList = null;

                    // check if we did actually complete
                    if (!_lockQueues.TryGetValue(resourceUri.ToString(), out waitingList)) {
                        return handleTaskSource.Task.Result;
                    }

                    // check if we did actually complete
                    if (!waitingList.Contains(handleTaskSource))
                        return handleTaskSource.Task.Result;
                }

                throw new TimeoutException("The lock could not be obtained within the timeout");
            } else {
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

            lock(_handles) {
                if (_handles.Contains(handle)) {
                    // remove
                    _handles.Remove((SlimLockHandle)handle);
                    removed = true;
                }
            }

            if (removed) {
                // invoke event
                ((SlimLockHandle)handle).OnInvalidated(this, new LockInvalidatedEventArgs() {
                    Handle = handle,
                    Reason = LockInvalidatedReason.Released
                });

                // check if any locks waiting
                lock(_lockQueues) {
                    // get waiting list or create
                    List<TaskCompletionSource<ILockHandle>> waitingList = null;

                    // check if we did actually complete
                    if (_lockQueues.TryGetValue(handle.ResourceURI.ToString(), out waitingList)) {
                        TaskCompletionSource<ILockHandle> waitingTask = waitingList[0];
                        waitingList.RemoveAt(0);

                        // create our handle
                        SlimLockHandle newHandle = new SlimLockHandle(this) {
                            ResourceURI = handle.ResourceURI
                        };

                        _handles.Add(newHandle);
                        waitingTask.TrySetResult(newHandle);

                        // remove
                        if (waitingList.Count == 0) {
                            _lockQueues.Remove(handle.ResourceURI.ToString());
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
        /// <returns>The lock handle or null if the lock could not be obtained.</returns>
        public Task<ILockHandle> LockAsync(string resourceUri, TimeSpan waitTime = default(TimeSpan)) {
            return LockAsync(new Uri(resourceUri), waitTime);
        }

        /// <summary>
        /// Gets if the resource is locked.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <returns>If the resource URI is locked.</returns>
        public Task<bool> IsLockedAsync(Uri resourceUri) {
            lock(_handles) {
                return Task.FromResult(_handles.Any(h => h.ResourceURI.ToString().Equals(resourceUri.ToString(), StringComparison.CurrentCultureIgnoreCase)));
            }
        }

        /// <summary>
        /// Gets if the resource is locked.
        /// </summary>
        /// <param name="resourceUri">The resource URI.</param>
        /// <returns>If the resource URI is locked.</returns>
        public Task<bool> IsLockedAsync(string resourceUri) {
            lock (_handles) {
                return Task.FromResult(_handles.Any(h => h.ResourceURI.ToString().Equals(resourceUri.ToString(), StringComparison.CurrentCultureIgnoreCase)));
            }
        }

        /// <summary>
        /// Creates an in-process locking manager.
        /// </summary>
        public SlimLockManager() {
        }
    }
}
