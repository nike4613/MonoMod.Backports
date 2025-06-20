﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#pragma warning disable 0420

// =+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+
//
// A spin lock is a mutual exclusion lock primitive where a thread trying to acquire the lock waits in a loop ("spins")
// repeatedly checking until the lock becomes available. As the thread remains active performing a non-useful task,
// the use of such a lock is a kind of busy waiting and consumes CPU resources without performing real work.
//
// =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Threading
{
    /// <summary>
    /// Provides a mutual exclusion lock primitive where a thread trying to acquire the lock waits in a loop
    /// repeatedly checking until the lock becomes available.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Spin locks can be used for leaf-level locks where the object allocation implied by using a <see
    /// cref="System.Threading.Monitor"/>, in size or due to garbage collection pressure, is overly
    /// expensive. Avoiding blocking is another reason that a spin lock can be useful, however if you expect
    /// any significant amount of blocking, you are probably best not using spin locks due to excessive
    /// spinning. Spinning can be beneficial when locks are fine grained and large in number (for example, a
    /// lock per node in a linked list) as well as when lock hold times are always extremely short. In
    /// general, while holding a spin lock, one should avoid blocking, calling anything that itself may
    /// block, holding more than one spin lock at once, making dynamically dispatched calls (interface and
    /// virtuals), making statically dispatched calls into any code one doesn't own, or allocating memory.
    /// </para>
    /// <para>
    /// <see cref="SpinLock"/> should only be used when it's been determined that doing so will improve an
    /// application's performance. It's also important to note that <see cref="SpinLock"/> is a value type,
    /// for performance reasons. As such, one must be very careful not to accidentally copy a SpinLock
    /// instance, as the two instances (the original and the copy) would then be completely independent of
    /// one another, which would likely lead to erroneous behavior of the application. If a SpinLock instance
    /// must be passed around, it should be passed by reference rather than by value.
    /// </para>
    /// <para>
    /// Do not store <see cref="SpinLock"/> instances in readonly fields.
    /// </para>
    /// <para>
    /// All members of <see cref="SpinLock"/> are thread-safe and may be used from multiple threads
    /// concurrently.
    /// </para>
    /// </remarks>
    [DebuggerTypeProxy(typeof(SystemThreading_SpinLockDebugView))]
    [DebuggerDisplay("IsHeld = {IsHeld}")]
    public struct SpinLock
    {
        // The current ownership state is a single signed int. There are two modes:
        //
        //    1) Ownership tracking enabled: the high bit is 0, and the remaining bits
        //       store the managed thread ID of the current owner.  When the 31 low bits
        //       are 0, the lock is available.
        //    2) Performance mode: when the high bit is 1, lock availability is indicated by the low bit.
        //       When the low bit is 1 -- the lock is held; 0 -- the lock is available.
        //
        // There are several masks and constants below for convenience.

        private volatile int _owner;

        // After how many yields, call Sleep(1)
        private const int SLEEP_ONE_FREQUENCY = 40;

        // After how many yields, check the timeout
        private const int TIMEOUT_CHECK_FREQUENCY = 10;

        // Thr thread tracking disabled mask
        private const int LOCK_ID_DISABLE_MASK = unchecked((int)0x80000000);        // 1000 0000 0000 0000 0000 0000 0000 0000

        // the lock is held by some thread, but we don't know which
        private const int LOCK_ANONYMOUS_OWNED = 0x1;                               // 0000 0000 0000 0000 0000 0000 0000 0001

        // Waiters mask if the thread tracking is disabled
        private const int WAITERS_MASK = ~(LOCK_ID_DISABLE_MASK | 1);               // 0111 1111 1111 1111 1111 1111 1111 1110

        // The Thread tacking is disabled and the lock bit is set, used in Enter fast path to make sure the id is disabled and lock is available
        private const int ID_DISABLED_AND_ANONYMOUS_OWNED = unchecked((int)0x80000001); // 1000 0000 0000 0000 0000 0000 0000 0001

        // If the thread is unowned if:
        // m_owner zero and the thread tracking is enabled
        // m_owner & LOCK_ANONYMOUS_OWNED = zero and the thread tracking is disabled
        private const int LOCK_UNOWNED = 0;

        // The maximum number of waiters (only used if the thread tracking is disabled)
        // The actual maximum waiters count is this number divided by two because each waiter increments the waiters count by 2
        // The waiters count is calculated by m_owner & WAITERS_MASK 01111....110
        private const int MAXIMUM_WAITERS = WAITERS_MASK;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompareExchange(ref int location, int value, int comparand, ref bool success)
        {
            int result = Interlocked.CompareExchange(ref location, value, comparand);
            success = (result == comparand);
            return result;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="System.Threading.SpinLock"/>
        /// structure with the option to track thread IDs to improve debugging.
        /// </summary>
        /// <remarks>
        /// The default constructor for <see cref="SpinLock"/> tracks thread ownership.
        /// </remarks>
        /// <param name="enableThreadOwnerTracking">Whether to capture and use thread IDs for debugging
        /// purposes.</param>
        public SpinLock(bool enableThreadOwnerTracking)
        {
            _owner = LOCK_UNOWNED;
            if (!enableThreadOwnerTracking)
            {
                _owner |= LOCK_ID_DISABLE_MASK;
                Debug.Assert(!IsThreadOwnerTrackingEnabled, "property should be false by now");
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="System.Threading.SpinLock"/>
        /// structure with the option to track thread IDs to improve debugging.
        /// </summary>
        /// <remarks>
        /// The default constructor for <see cref="SpinLock"/> tracks thread ownership.
        /// </remarks>
        /// <summary>
        /// Acquires the lock in a reliable manner, such that even if an exception occurs within the method
        /// call, <paramref name="lockTaken"/> can be examined reliably to determine whether the lock was
        /// acquired.
        /// </summary>
        /// <remarks>
        /// <see cref="SpinLock"/> is a non-reentrant lock, meaning that if a thread holds the lock, it is
        /// not allowed to enter the lock again. If thread ownership tracking is enabled (whether it's
        /// enabled is available through <see cref="IsThreadOwnerTrackingEnabled"/>), an exception will be
        /// thrown when a thread tries to re-enter a lock it already holds. However, if thread ownership
        /// tracking is disabled, attempting to enter a lock already held will result in deadlock.
        /// </remarks>
        /// <param name="lockTaken">True if the lock is acquired; otherwise, false. <paramref
        /// name="lockTaken"/> must be initialized to false prior to calling this method.</param>
        /// <exception cref="System.Threading.LockRecursionException">
        /// Thread ownership tracking is enabled, and the current thread has already acquired this lock.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// The <paramref name="lockTaken"/> argument must be initialized to false prior to calling Enter.
        /// </exception>
        public void Enter(ref bool lockTaken)
        {
            // Try to keep the code and branching in this method as small as possible in order to inline the method
            int observedOwner = _owner;
            if (lockTaken || // invalid parameter
                (observedOwner & ID_DISABLED_AND_ANONYMOUS_OWNED) != LOCK_ID_DISABLE_MASK || // thread tracking is enabled or the lock is already acquired
                CompareExchange(ref _owner, observedOwner | LOCK_ANONYMOUS_OWNED, observedOwner, ref lockTaken) != observedOwner) // acquiring the lock failed
                ContinueTryEnter(Timeout.Infinite, ref lockTaken); // Then try the slow path if any of the above conditions is met
        }

        /// <summary>
        /// Attempts to acquire the lock in a reliable manner, such that even if an exception occurs within
        /// the method call, <paramref name="lockTaken"/> can be examined reliably to determine whether the
        /// lock was acquired.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="Enter"/>, TryEnter will not block waiting for the lock to be available. If the
        /// lock is not available when TryEnter is called, it will return immediately without any further
        /// spinning.
        /// </remarks>
        /// <param name="lockTaken">True if the lock is acquired; otherwise, false. <paramref
        /// name="lockTaken"/> must be initialized to false prior to calling this method.</param>
        /// <exception cref="System.Threading.LockRecursionException">
        /// Thread ownership tracking is enabled, and the current thread has already acquired this lock.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// The <paramref name="lockTaken"/> argument must be initialized to false prior to calling TryEnter.
        /// </exception>
        public void TryEnter(ref bool lockTaken)
        {
            int observedOwner = _owner;
            if (((observedOwner & LOCK_ID_DISABLE_MASK) == 0) | lockTaken)
            {
                // Thread tracking enabled or invalid arg. Take slow path.
                ContinueTryEnter(0, ref lockTaken);
            }
            else if ((observedOwner & LOCK_ANONYMOUS_OWNED) != 0)
            {
                // Lock already held by someone
                lockTaken = false;
            }
            else
            {
                // Lock wasn't held; try to acquire it.
                CompareExchange(ref _owner, observedOwner | LOCK_ANONYMOUS_OWNED, observedOwner, ref lockTaken);
            }
        }

        /// <summary>
        /// Attempts to acquire the lock in a reliable manner, such that even if an exception occurs within
        /// the method call, <paramref name="lockTaken"/> can be examined reliably to determine whether the
        /// lock was acquired.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="Enter"/>, TryEnter will not block indefinitely waiting for the lock to be
        /// available. It will block until either the lock is available or until the <paramref
        /// name="timeout"/>
        /// has expired.
        /// </remarks>
        /// <param name="timeout">A <see cref="System.TimeSpan"/> that represents the number of milliseconds
        /// to wait, or a <see cref="System.TimeSpan"/> that represents -1 milliseconds to wait indefinitely.
        /// </param>
        /// <param name="lockTaken">True if the lock is acquired; otherwise, false. <paramref
        /// name="lockTaken"/> must be initialized to false prior to calling this method.</param>
        /// <exception cref="System.Threading.LockRecursionException">
        /// Thread ownership tracking is enabled, and the current thread has already acquired this lock.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// The <paramref name="lockTaken"/> argument must be initialized to false prior to calling TryEnter.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="timeout"/> is a negative
        /// number other than -1 milliseconds, which represents an infinite time-out -or- timeout is greater
        /// than <see cref="int.MaxValue"/> milliseconds.
        /// </exception>
        public void TryEnter(TimeSpan timeout, ref bool lockTaken)
        {
            // Validate the timeout
            long totalMilliseconds = (long)timeout.TotalMilliseconds;
            if (totalMilliseconds < -1 || totalMilliseconds > int.MaxValue)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(timeout), timeout, "Timeout was too long!");
            }

            // Call reliable enter with the int-based timeout milliseconds
            TryEnter((int)timeout.TotalMilliseconds, ref lockTaken);
        }

        /// <summary>
        /// Attempts to acquire the lock in a reliable manner, such that even if an exception occurs within
        /// the method call, <paramref name="lockTaken"/> can be examined reliably to determine whether the
        /// lock was acquired.
        /// </summary>
        /// <remarks>
        /// Unlike <see cref="Enter"/>, TryEnter will not block indefinitely waiting for the lock to be
        /// available. It will block until either the lock is available or until the <paramref
        /// name="millisecondsTimeout"/> has expired.
        /// </remarks>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or <see
        /// cref="System.Threading.Timeout.Infinite"/> (-1) to wait indefinitely.</param>
        /// <param name="lockTaken">True if the lock is acquired; otherwise, false. <paramref
        /// name="lockTaken"/> must be initialized to false prior to calling this method.</param>
        /// <exception cref="System.Threading.LockRecursionException">
        /// Thread ownership tracking is enabled, and the current thread has already acquired this lock.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// The <paramref name="lockTaken"/> argument must be initialized to false prior to calling TryEnter.
        /// </exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="millisecondsTimeout"/> is
        /// a negative number other than -1, which represents an infinite time-out.</exception>
        public void TryEnter(int millisecondsTimeout, ref bool lockTaken)
        {
            int observedOwner = _owner;
            if (millisecondsTimeout < -1 || // invalid parameter
                lockTaken || // invalid parameter
                (observedOwner & ID_DISABLED_AND_ANONYMOUS_OWNED) != LOCK_ID_DISABLE_MASK ||  // thread tracking is enabled or the lock is already acquired
                CompareExchange(ref _owner, observedOwner | LOCK_ANONYMOUS_OWNED, observedOwner, ref lockTaken) != observedOwner) // acquiring the lock failed
                ContinueTryEnter(millisecondsTimeout, ref lockTaken); // The call the slow pth
        }

        /// <summary>
        /// Try acquire the lock with long path, this is usually called after the first path in Enter and
        /// TryEnter failed The reason for short path is to make it inline in the run time which improves the
        /// performance. This method assumed that the parameter are validated in Enter or TryEnter method.
        /// </summary>
        /// <param name="millisecondsTimeout">The timeout milliseconds</param>
        /// <param name="lockTaken">The lockTaken param</param>
        private void ContinueTryEnter(int millisecondsTimeout, ref bool lockTaken)
        {
            // The fast path doesn't throw any exception, so we have to validate the parameters here
            if (lockTaken)
            {
                lockTaken = false;
                throw new ArgumentException("Lock was already taken");
            }

            if (millisecondsTimeout < -1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(millisecondsTimeout), millisecondsTimeout, "Timeout too small");
            }

            uint startTime = 0;
            if (millisecondsTimeout != Timeout.Infinite && millisecondsTimeout != 0)
            {
                startTime = TimeoutHelper.GetTime();
            }

            if (IsThreadOwnerTrackingEnabled)
            {
                // Slow path for enabled thread tracking mode
                ContinueTryEnterWithThreadTracking(millisecondsTimeout, startTime, ref lockTaken);
                return;
            }

            // then thread tracking is disabled
            // In this case there are three ways to acquire the lock
            // 1- the first way the thread either tries to get the lock if it's free or updates the waiters, if the turn >= the processors count then go to 3 else go to 2
            // 2- In this step the waiter threads spins and tries to acquire the lock, the number of spin iterations and spin count is dependent on the thread turn
            // the late the thread arrives the more it spins and less frequent it check the lock availability
            // Also the spins count is increases each iteration
            // If the spins iterations finished and failed to acquire the lock, go to step 3
            // 3- This is the yielding step, there are two ways of yielding Thread.Yield and Sleep(1)
            // If the timeout is expired in after step 1, we need to decrement the waiters count before returning

            int observedOwner;
            int turn = int.MaxValue;
            // ***Step 1, take the lock or update the waiters

            // try to acquire the lock directly if possible or update the waiters count
            observedOwner = _owner;
            if ((observedOwner & LOCK_ANONYMOUS_OWNED) == LOCK_UNOWNED)
            {
                if (CompareExchange(ref _owner, observedOwner | 1, observedOwner, ref lockTaken) == observedOwner)
                {
                    // Acquired lock
                    return;
                }

                if (millisecondsTimeout == 0)
                {
                    // Did not acquire lock in CompareExchange and timeout is 0 so fail fast
                    return;
                }
            }
            else if (millisecondsTimeout == 0)
            {
                // Did not acquire lock as owned and timeout is 0 so fail fast
                return;
            }
            else // failed to acquire the lock, then try to update the waiters. If the waiters count reached the maximum, just break the loop to avoid overflow
            {
                if ((observedOwner & WAITERS_MASK) != MAXIMUM_WAITERS)
                {
                    // This can still overflow, but maybe there will never be that many waiters
                    turn = (Interlocked.Add(ref _owner, 2) & WAITERS_MASK) >> 1;
                }
            }

            // lock acquired failed and waiters updated

            // *** Step 2, Spinning and Yielding
            SpinWait spinner = default;
            if (turn > Environment.ProcessorCount)
            {
                spinner.Count = SpinWait.YieldThreshold;
            }
            while (true)
            {
                spinner.SpinOnce(SLEEP_ONE_FREQUENCY);

                observedOwner = _owner;
                if ((observedOwner & LOCK_ANONYMOUS_OWNED) == LOCK_UNOWNED)
                {
                    int newOwner = (observedOwner & WAITERS_MASK) == 0 ? // Gets the number of waiters, if zero
                           observedOwner | 1 // don't decrement it. just set the lock bit, it is zero because a previous call of Exit(false) which corrupted the waiters
                           : (observedOwner - 2) | 1; // otherwise decrement the waiters and set the lock bit
                    Debug.Assert((newOwner & WAITERS_MASK) >= 0);

                    if (CompareExchange(ref _owner, newOwner, observedOwner, ref lockTaken) == observedOwner)
                    {
                        return;
                    }
                }

                if (spinner.Count % TIMEOUT_CHECK_FREQUENCY == 0)
                {
                    // Check the timeout.
                    if (millisecondsTimeout != Timeout.Infinite && TimeoutHelper.UpdateTimeOut(startTime, millisecondsTimeout) <= 0)
                    {
                        DecrementWaiters();
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// decrements the waiters, in case of the timeout is expired
        /// </summary>
        private void DecrementWaiters()
        {
            SpinWait spinner = default;
            while (true)
            {
                int observedOwner = _owner;
                if ((observedOwner & WAITERS_MASK) == 0)
                    return; // don't decrement the waiters if it's corrupted by previous call of Exit(false)
                if (Interlocked.CompareExchange(ref _owner, observedOwner - 2, observedOwner) == observedOwner)
                {
                    Debug.Assert(!IsThreadOwnerTrackingEnabled); // Make sure the waiters never be negative which will cause the thread tracking bit to be flipped
                    break;
                }
                spinner.SpinOnce();
            }
        }

        /// <summary>
        /// ContinueTryEnter for the thread tracking mode enabled
        /// </summary>
        private void ContinueTryEnterWithThreadTracking(int millisecondsTimeout, uint startTime, ref bool lockTaken)
        {
            Debug.Assert(IsThreadOwnerTrackingEnabled);

            const int LockUnowned = 0;
            // We are using thread IDs to mark ownership. Snap the thread ID and check for recursion.
            // We also must or the ID enablement bit, to ensure we propagate when we CAS it in.
            int newOwner = Thread.CurrentThread.ManagedThreadId;
            if (_owner == newOwner)
            {
                // We don't allow lock recursion.
                throw new LockRecursionException("Recursive lock enter");
            }

            SpinWait spinner = default;

            // Loop until the lock has been successfully acquired or, if specified, the timeout expires.
            while (true)
            {
                // We failed to get the lock, either from the fast route or the last iteration
                // and the timeout hasn't expired; spin once and try again.
                spinner.SpinOnce();

                // Test before trying to CAS, to avoid acquiring the line exclusively unnecessarily.

                if (_owner == LockUnowned)
                {
                    if (CompareExchange(ref _owner, newOwner, LockUnowned, ref lockTaken) == LockUnowned)
                    {
                        return;
                    }
                }
                // Check the timeout.  We only RDTSC if the next spin will yield, to amortize the cost.
                if (millisecondsTimeout == 0 ||
                    (millisecondsTimeout != Timeout.Infinite && spinner.NextSpinWillYield &&
                    TimeoutHelper.UpdateTimeOut(startTime, millisecondsTimeout) <= 0))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        /// <remarks>
        /// The default overload of <see cref="Exit()"/> provides the same behavior as if calling <see
        /// cref="Exit(bool)"/> using true as the argument, but Exit() could be slightly faster than Exit(true).
        /// </remarks>
        /// <exception cref="SynchronizationLockException">
        /// Thread ownership tracking is enabled, and the current thread is not the owner of this lock.
        /// </exception>
        public void Exit()
        {
            // This is the fast path for the thread tracking is disabled, otherwise go to the slow path
            if ((_owner & LOCK_ID_DISABLE_MASK) == 0)
                ExitSlowPath(true);
            else
                Interlocked.Decrement(ref _owner);
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        /// <param name="useMemoryBarrier">
        /// A Boolean value that indicates whether a memory fence should be issued in order to immediately
        /// publish the exit operation to other threads.
        /// </param>
        /// <remarks>
        /// Calling <see cref="Exit(bool)"/> with the <paramref name="useMemoryBarrier"/> argument set to
        /// true will improve the fairness of the lock at the expense of some performance. The default <see
        /// cref="Enter"/>
        /// overload behaves as if specifying true for <paramref name="useMemoryBarrier"/>.
        /// </remarks>
        /// <exception cref="SynchronizationLockException">
        /// Thread ownership tracking is enabled, and the current thread is not the owner of this lock.
        /// </exception>
        public void Exit(bool useMemoryBarrier)
        {
            // This is the fast path for the thread tracking is disabled and not to use memory barrier, otherwise go to the slow path
            // The reason not to add else statement if the usememorybarrier is that it will add more branching in the code and will prevent
            // method inlining, so this is optimized for useMemoryBarrier=false and Exit() overload optimized for useMemoryBarrier=true.
            int tmpOwner = _owner;
            if ((tmpOwner & LOCK_ID_DISABLE_MASK) != 0 & !useMemoryBarrier)
            {
                _owner = tmpOwner & (~LOCK_ANONYMOUS_OWNED);
            }
            else
            {
                ExitSlowPath(useMemoryBarrier);
            }
        }

        /// <summary>
        /// The slow path for exit method if the fast path failed
        /// </summary>
        /// <param name="useMemoryBarrier">
        /// A Boolean value that indicates whether a memory fence should be issued in order to immediately
        /// publish the exit operation to other threads
        /// </param>
        private void ExitSlowPath(bool useMemoryBarrier)
        {
            bool threadTrackingEnabled = (_owner & LOCK_ID_DISABLE_MASK) == 0;
            if (threadTrackingEnabled && !IsHeldByCurrentThread)
            {
                throw new SynchronizationLockException("Lock released by thread that doesn't own it");
            }

            if (useMemoryBarrier)
            {
                if (threadTrackingEnabled)
                {
                    Interlocked.Exchange(ref _owner, LOCK_UNOWNED);
                }
                else
                {
                    Interlocked.Decrement(ref _owner);
                }
            }
            else
            {
                if (threadTrackingEnabled)
                {
                    _owner = LOCK_UNOWNED;
                }
                else
                {
                    int tmpOwner = _owner;
                    _owner = tmpOwner & (~LOCK_ANONYMOUS_OWNED);
                }
            }
        }

        /// <summary>
        /// Gets whether the lock is currently held by any thread.
        /// </summary>
        public bool IsHeld
        {
            get
            {
                if (IsThreadOwnerTrackingEnabled)
                    return _owner != LOCK_UNOWNED;

                return (_owner & LOCK_ANONYMOUS_OWNED) != LOCK_UNOWNED;
            }
        }

        /// <summary>
        /// Gets whether the lock is currently held by any thread.
        /// </summary>
        /// <summary>
        /// Gets whether the lock is held by the current thread.
        /// </summary>
        /// <remarks>
        /// If the lock was initialized to track owner threads, this will return whether the lock is acquired
        /// by the current thread. It is invalid to use this property when the lock was initialized to not
        /// track thread ownership.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">
        /// Thread ownership tracking is disabled.
        /// </exception>
        public bool IsHeldByCurrentThread
        {
            get
            {
                if (!IsThreadOwnerTrackingEnabled)
                {
                    throw new InvalidOperationException("Thread owner tracking isn't enabled");
                }
                return (_owner & (~LOCK_ID_DISABLE_MASK)) == Thread.CurrentThread.ManagedThreadId;
            }
        }

        /// <summary>Gets whether thread ownership tracking is enabled for this instance.</summary>
        public bool IsThreadOwnerTrackingEnabled => (_owner & LOCK_ID_DISABLE_MASK) == 0;

        #region Debugger proxy class
        /// <summary>
        /// internal class used by debug type proxy attribute to display the owner thread ID
        /// </summary>
        internal sealed class SystemThreading_SpinLockDebugView
        {
            // SpinLock object
            private SpinLock _spinLock;

            /// <summary>
            /// SystemThreading_SpinLockDebugView constructor
            /// </summary>
            /// <param name="spinLock">The SpinLock to be proxied.</param>
            public SystemThreading_SpinLockDebugView(SpinLock spinLock)
            {
                // Note that this makes a copy of the SpinLock (struct). It doesn't hold a reference to it.
                _spinLock = spinLock;
            }

            /// <summary>
            /// Checks if the lock is held by the current thread or not
            /// </summary>
            public bool? IsHeldByCurrentThread
            {
                get
                {
                    try
                    {
                        return _spinLock.IsHeldByCurrentThread;
                    }
                    catch (InvalidOperationException)
                    {
                        return null;
                    }
                }
            }

            /// <summary>
            /// Gets the current owner thread, zero if it is released
            /// </summary>
            public int? OwnerThreadID
            {
                get
                {
                    if (_spinLock.IsThreadOwnerTrackingEnabled)
                    {
                        return _spinLock._owner;
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            /// <summary>
            ///  Gets whether the lock is currently held by any thread or not.
            /// </summary>
            public bool IsHeld => _spinLock.IsHeld;
        }
        #endregion

    }

    // SpinWait is just a little value type that encapsulates some common spinning
    // logic. It ensures we always yield on single-proc machines (instead of using busy
    // waits), and that we work well on HT. It encapsulates a good mixture of spinning
    // and real yielding. It's a value type so that various areas of the engine can use
    // one by allocating it on the stack w/out unnecessary GC allocation overhead, e.g.:
    //
    //     void f() {
    //         SpinWait wait = new SpinWait();
    //         while (!p) { wait.SpinOnce(); }
    //         ...
    //     }
    //
    // Internally it just maintains a counter that is used to decide when to yield, etc.
    //
    // A common usage is to spin before blocking. In those cases, the NextSpinWillYield
    // property allows a user to decide to fall back to waiting once it returns true:
    //
    //     void f() {
    //         SpinWait wait = new SpinWait();
    //         while (!p) {
    //             if (wait.NextSpinWillYield) { /* block! */ }
    //             else { wait.SpinOnce(); }
    //         }
    //         ...
    //     }

    /// <summary>
    /// Provides support for spin-based waiting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SpinWait"/> encapsulates common spinning logic. On single-processor machines, yields are
    /// always used instead of busy waits, and on computers with Intel(R) processors employing Hyper-Threading
    /// technology, it helps to prevent hardware thread starvation. SpinWait encapsulates a good mixture of
    /// spinning and true yielding.
    /// </para>
    /// <para>
    /// <see cref="SpinWait"/> is a value type, which means that low-level code can utilize SpinWait without
    /// fear of unnecessary allocation overheads. SpinWait is not generally useful for ordinary applications.
    /// In most cases, you should use the synchronization classes provided by the .NET Framework, such as
    /// <see cref="System.Threading.Monitor"/>. For most purposes where spin waiting is required, however,
    /// the <see cref="SpinWait"/> type should be preferred over the <see
    /// cref="System.Threading.Thread.SpinWait"/> method.
    /// </para>
    /// <para>
    /// While SpinWait is designed to be used in concurrent applications, it is not designed to be
    /// used from multiple threads concurrently.  SpinWait's members are not thread-safe.  If multiple
    /// threads must spin, each should use its own instance of SpinWait.
    /// </para>
    /// </remarks>
    public struct SpinWait
    {
        // These constants determine the frequency of yields versus spinning. The
        // numbers may seem fairly arbitrary, but were derived with at least some
        // thought in the design document.  I fully expect they will need to change
        // over time as we gain more experience with performance.
        internal const int YieldThreshold = 10; // When to switch over to a true yield.
        private const int Sleep0EveryHowManyYields = 5; // After how many yields should we Sleep(0)?
        internal const int DefaultSleep1Threshold = 20; // After how many yields should we Sleep(1) frequently?

        /// <summary>
        /// A suggested number of spin iterations before doing a proper wait, such as waiting on an event that becomes signaled
        /// when the resource becomes available.
        /// </summary>
        /// <remarks>
        /// These numbers were arrived at by experimenting with different numbers in various cases that currently use it. It's
        /// only a suggested value and typically works well when the proper wait is something like an event.
        ///
        /// Spinning less can lead to early waiting and more context switching, spinning more can decrease latency but may use
        /// up some CPU time unnecessarily. Depends on the situation too, for instance SemaphoreSlim uses more iterations
        /// because the waiting there is currently a lot more expensive (involves more spinning, taking a lock, etc.). It also
        /// depends on the likelihood of the spin being successful and how long the wait would be but those are not accounted
        /// for here.
        /// </remarks>
        internal static readonly int SpinCountforSpinBeforeWait = Environment.ProcessorCount == 1 ? 1 : 35;

        // The number of times we've spun already.
        private int _count;

        /// <summary>
        /// Gets the number of times <see cref="SpinOnce()"/> has been called on this instance.
        /// </summary>
        public int Count
        {
            get => _count;
            internal set
            {
                Debug.Assert(value >= 0);
                _count = value;
            }
        }

        /// <summary>
        /// Gets whether the next call to <see cref="SpinOnce()"/> will yield the processor, triggering a
        /// forced context switch.
        /// </summary>
        /// <value>Whether the next call to <see cref="SpinOnce()"/> will yield the processor, triggering a
        /// forced context switch.</value>
        /// <remarks>
        /// On a single-CPU machine, <see cref="SpinOnce()"/> always yields the processor. On machines with
        /// multiple CPUs, <see cref="SpinOnce()"/> may yield after an unspecified number of calls.
        /// </remarks>
        public bool NextSpinWillYield => _count >= YieldThreshold || Environment.ProcessorCount == 1;

        /// <summary>
        /// Performs a single spin.
        /// </summary>
        /// <remarks>
        /// This is typically called in a loop, and may change in behavior based on the number of times a
        /// <see cref="SpinOnce()"/> has been called thus far on this instance.
        /// </remarks>
        public void SpinOnce()
        {
            SpinOnceCore(DefaultSleep1Threshold);
        }

        // SpinOnce(int) is only available in Core 3+
        /// <summary>
        /// Performs a single spin.
        /// </summary>
        /// <param name="sleep1Threshold">
        /// A minimum spin count after which <code>Thread.Sleep(1)</code> may be used. A value of <code>-1</code> may be used to
        /// disable the use of <code>Thread.Sleep(1)</code>.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="sleep1Threshold"/> is less than <code>-1</code>.
        /// </exception>
        /// <remarks>
        /// This is typically called in a loop, and may change in behavior based on the number of times a
        /// <see cref="SpinOnce()"/> has been called thus far on this instance.
        /// </remarks>
        internal void SpinOnce(int sleep1Threshold)
        {
            if (sleep1Threshold < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(sleep1Threshold), sleep1Threshold, "Need positive or -1 value");
            }

            if (sleep1Threshold >= 0 && sleep1Threshold < YieldThreshold)
            {
                sleep1Threshold = YieldThreshold;
            }

            SpinOnceCore(sleep1Threshold);
        }

        private void SpinOnceCore(int sleep1Threshold)
        {
            Debug.Assert(sleep1Threshold >= -1);
            Debug.Assert(sleep1Threshold < 0 || sleep1Threshold >= YieldThreshold);

            // (_count - YieldThreshold) % 2 == 0: The purpose of this check is to interleave Thread.Yield/Sleep(0) with
            // Thread.SpinWait. Otherwise, the following issues occur:
            //   - When there are no threads to switch to, Yield and Sleep(0) become no-op and it turns the spin loop into a
            //     busy-spin that may quickly reach the max spin count and cause the thread to enter a wait state, or may
            //     just busy-spin for longer than desired before a Sleep(1). Completing the spin loop too early can cause
            //     excessive context switcing if a wait follows, and entering the Sleep(1) stage too early can cause
            //     excessive delays.
            //   - If there are multiple threads doing Yield and Sleep(0) (typically from the same spin loop due to
            //     contention), they may switch between one another, delaying work that can make progress.
            if ((
                    _count >= YieldThreshold &&
                    ((_count >= sleep1Threshold && sleep1Threshold >= 0) || (_count - YieldThreshold) % 2 == 0)
                ) ||
                Environment.ProcessorCount == 1)
            {
                //
                // We must yield.
                //
                // We prefer to call Thread.Yield first, triggering a SwitchToThread. This
                // unfortunately doesn't consider all runnable threads on all OS SKUs. In
                // some cases, it may only consult the runnable threads whose ideal processor
                // is the one currently executing code. Thus we occasionally issue a call to
                // Sleep(0), which considers all runnable threads at equal priority. Even this
                // is insufficient since we may be spin waiting for lower priority threads to
                // execute; we therefore must call Sleep(1) once in a while too, which considers
                // all runnable threads, regardless of ideal processor and priority, but may
                // remove the thread from the scheduler's queue for 10+ms, if the system is
                // configured to use the (default) coarse-grained system timer.
                //

                if (_count >= sleep1Threshold && sleep1Threshold >= 0)
                {
                    Thread.Sleep(1);
                }
                else
                {
                    int yieldsSoFar = _count >= YieldThreshold ? (_count - YieldThreshold) / 2 : _count;
                    if ((yieldsSoFar % Sleep0EveryHowManyYields) == (Sleep0EveryHowManyYields - 1))
                    {
                        Thread.Sleep(0);
                    }
                    else
                    {
                        // Unfortunately, .NET 3.5 doesn't have Thread.Yield, so we always Sleep(0)
                        Thread.Sleep(0);
                        //Thread.Yield();
                    }
                }
            }
            else
            {
                //
                // Otherwise, we will spin.
                //
                // We do this using the CLR's SpinWait API, which is just a busy loop that
                // issues YIELD/PAUSE instructions to ensure multi-threaded CPUs can react
                // intelligently to avoid starving. (These are NOOPs on other CPUs.) We
                // choose a number for the loop iteration count such that each successive
                // call spins for longer, to reduce cache contention.  We cap the total
                // number of spins we are willing to tolerate to reduce delay to the caller,
                // since we expect most callers will eventually block anyway.
                //
                // Also, cap the maximum spin count to a value such that many thousands of CPU cycles would not be wasted doing
                // the equivalent of YieldProcessor(), as at that point SwitchToThread/Sleep(0) are more likely to be able to
                // allow other useful work to run. Long YieldProcessor() loops can help to reduce contention, but Sleep(1) is
                // usually better for that.
                int n = 7; // this seems to be what is used
                if (_count <= 30 && (1 << _count) < n)
                {
                    n = 1 << _count;
                }
                Thread.SpinWait(n);
            }

            // Finally, increment our spin counter.
            _count = (_count == int.MaxValue ? YieldThreshold : _count + 1);
        }

        /// <summary>
        /// Resets the spin counter.
        /// </summary>
        /// <remarks>
        /// This makes <see cref="SpinOnce()"/> and <see cref="NextSpinWillYield"/> behave as though no calls
        /// to <see cref="SpinOnce()"/> had been issued on this instance. If a <see cref="SpinWait"/> instance
        /// is reused many times, it may be useful to reset it to avoid yielding too soon.
        /// </remarks>
        public void Reset()
        {
            _count = 0;
        }

        #region Static Methods
        /// <summary>
        /// Spins until the specified condition is satisfied.
        /// </summary>
        /// <param name="condition">A delegate to be executed over and over until it returns true.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="condition"/> argument is null.</exception>
        public static void SpinUntil(Func<bool> condition)
        {
#if DEBUG
            bool result =
#endif
            SpinUntil(condition, Timeout.Infinite);
#if DEBUG
            Debug.Assert(result);
#endif
        }

        /// <summary>
        /// Spins until the specified condition is satisfied or until the specified timeout is expired.
        /// </summary>
        /// <param name="condition">A delegate to be executed over and over until it returns true.</param>
        /// <param name="timeout">
        /// A <see cref="TimeSpan"/> that represents the number of milliseconds to wait,
        /// or a TimeSpan that represents -1 milliseconds to wait indefinitely.</param>
        /// <returns>True if the condition is satisfied within the timeout; otherwise, false</returns>
        /// <exception cref="ArgumentNullException">The <paramref name="condition"/> argument is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="timeout"/> is a negative number
        /// other than -1 milliseconds, which represents an infinite time-out -or- timeout is greater than
        /// <see cref="int.MaxValue"/>.</exception>
        public static bool SpinUntil(Func<bool> condition, TimeSpan timeout)
        {
            // Validate the timeout
            long totalMilliseconds = (long)timeout.TotalMilliseconds;
            if (totalMilliseconds < -1 || totalMilliseconds > int.MaxValue)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(timeout), timeout, "Timeout out of range");
            }

            // Call wait with the timeout milliseconds
            return SpinUntil(condition, (int)totalMilliseconds);
        }

        /// <summary>
        /// Spins until the specified condition is satisfied or until the specified timeout is expired.
        /// </summary>
        /// <param name="condition">A delegate to be executed over and over until it returns true.</param>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or <see
        /// cref="System.Threading.Timeout.Infinite"/> (-1) to wait indefinitely.</param>
        /// <returns>True if the condition is satisfied within the timeout; otherwise, false</returns>
        /// <exception cref="ArgumentNullException">The <paramref name="condition"/> argument is null.</exception>
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="millisecondsTimeout"/> is a
        /// negative number other than -1, which represents an infinite time-out.</exception>
        public static bool SpinUntil(Func<bool> condition, int millisecondsTimeout)
        {
            if (millisecondsTimeout < Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(millisecondsTimeout), millisecondsTimeout, "Timeout out of range");
            }
            if (condition is null)
            {
                throw new ArgumentNullException(nameof(condition));
            }
            uint startTime = 0;
            if (millisecondsTimeout != 0 && millisecondsTimeout != Timeout.Infinite)
            {
                startTime = TimeoutHelper.GetTime();
            }
            SpinWait spinner = default;
            while (!condition())
            {
                if (millisecondsTimeout == 0)
                {
                    return false;
                }

                spinner.SpinOnce();

                if (millisecondsTimeout != Timeout.Infinite && spinner.NextSpinWillYield)
                {
                    if (millisecondsTimeout <= (TimeoutHelper.GetTime() - startTime))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        #endregion
    }

    /// <summary>
    /// A helper class to capture a start time using <see cref="Environment.TickCount"/> as a time in milliseconds.
    /// Also updates a given timeout by subtracting the current time from the start time.
    /// </summary>
    internal static class TimeoutHelper
    {
        /// <summary>
        /// Returns <see cref="Environment.TickCount"/> as a start time in milliseconds as a <see cref="uint"/>.
        /// <see cref="Environment.TickCount"/> rolls over from positive to negative every ~25 days, then ~25 days to back to positive again.
        /// <see cref="uint"/> is used to ignore the sign and double the range to 50 days.
        /// </summary>
        public static uint GetTime()
        {
            return (uint)Environment.TickCount;
        }

        /// <summary>
        /// Helper function to measure and update the elapsed time
        /// </summary>
        /// <param name="startTime"> The first time (in milliseconds) observed when the wait started</param>
        /// <param name="originalWaitMillisecondsTimeout">The original wait timeout in milliseconds</param>
        /// <returns>The new wait time in milliseconds, or -1 if the time expired</returns>
        public static int UpdateTimeOut(uint startTime, int originalWaitMillisecondsTimeout)
        {
            // The function must be called in case the time out is not infinite
            Debug.Assert(originalWaitMillisecondsTimeout != Timeout.Infinite);

            uint elapsedMilliseconds = (GetTime() - startTime);

            // Check the elapsed milliseconds is greater than max int because this property is uint
            if (elapsedMilliseconds > int.MaxValue)
            {
                return 0;
            }

            // Subtract the elapsed time from the current wait time
            int currentWaitTimeout = originalWaitMillisecondsTimeout - (int)elapsedMilliseconds;
            if (currentWaitTimeout <= 0)
            {
                return 0;
            }

            return currentWaitTimeout;
        }
    }
}
#pragma warning restore 0420