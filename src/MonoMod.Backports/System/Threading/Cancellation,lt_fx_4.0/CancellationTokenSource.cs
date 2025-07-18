// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Threading
{
    /// <summary>Signals to a <see cref="CancellationToken"/> that it should be canceled.</summary>
    /// <remarks>
    /// <para>
    /// <see cref="CancellationTokenSource"/> is used to instantiate a <see cref="CancellationToken"/> (via
    /// the source's <see cref="Token">Token</see> property) that can be handed to operations that wish to be
    /// notified of cancellation or that can be used to register asynchronous operations for cancellation. That
    /// token may have cancellation requested by calling to the source's <see cref="Cancel()"/> method.
    /// </para>
    /// <para>
    /// All members of this class, except <see cref="Dispose()"/>, are thread-safe and may be used
    /// concurrently from multiple threads.
    /// </para>
    /// </remarks>
    [DebuggerDisplay("IsCancellationRequested = {IsCancellationRequested}")]
    public class CancellationTokenSource : IDisposable
    {
        /// <summary>A <see cref="CancellationTokenSource"/> that's already canceled.</summary>
        internal static readonly CancellationTokenSource s_canceledSource = new CancellationTokenSource() { _state = (int)States.NotifyingCompleteState };
        /// <summary>A <see cref="CancellationTokenSource"/> that's never canceled.  This isn't enforced programmatically, only by usage.  Do not cancel!</summary>
        internal static readonly CancellationTokenSource s_neverCanceledSource = new CancellationTokenSource();

        /// <summary>Delegate used with <see cref="Timer"/> to trigger cancellation of a <see cref="CancellationTokenSource"/>.</summary>
        private static readonly TimerCallback s_timerCallback = TimerCallback;
        private static void TimerCallback(object? state) => // separated out into a named method to improve Timer diagnostics in a debugger
            ((CancellationTokenSource)state!).NotifyCancellation(throwOnFirstException: false); // skip ThrowIfDisposed() check in Cancel()

        /// <summary>The current state of the CancellationTokenSource.</summary>
        private volatile /*States*/int _state;
        /// <summary>Whether this <see cref="CancellationTokenSource"/> has been disposed.</summary>
        private bool _disposed;
        /// <summary>ITimer used by CancelAfter and Timer-related ctors. Used instead of Timer to avoid extra allocations and because the rooted behavior is desired.</summary>
        private volatile Timer? _timer;
        /// <summary><see cref="Threading.WaitHandle"/> lazily initialized and returned from <see cref="WaitHandle"/>.</summary>
        private volatile ManualResetEvent? _kernelEvent;
        /// <summary>Registration state for the source.</summary>
        /// <remarks>Lazily-initialized, also serving as the lock to protect its contained state.</remarks>
        private Registrations? _registrations;

        /// <summary>Legal values for <see cref="_state"/>.</summary>
        private enum States
        {
            NotCanceledState = 0, // default value of _state
            NotifyingState = 1,
            NotifyingCompleteState = 2,
        }

        /// <summary>Gets whether cancellation has been requested for this <see cref="CancellationTokenSource" />.</summary>
        /// <value>Whether cancellation has been requested for this <see cref="CancellationTokenSource" />.</value>
        /// <remarks>
        /// <para>
        /// This property indicates whether cancellation has been requested for this token source, such as
        /// due to a call to its <see cref="Cancel()"/> method.
        /// </para>
        /// <para>
        /// If this property returns true, it only guarantees that cancellation has been requested. It does not
        /// guarantee that every handler registered with the corresponding token has finished executing, nor
        /// that cancellation requests have finished propagating to all registered handlers. Additional
        /// synchronization may be required, particularly in situations where related objects are being
        /// canceled concurrently.
        /// </para>
        /// </remarks>
        public bool IsCancellationRequested => _state != (int)States.NotCanceledState;

        /// <summary>A simple helper to determine whether cancellation has finished.</summary>
        internal bool IsCancellationCompleted => _state == (int)States.NotifyingCompleteState;

        /// <summary>Gets the <see cref="CancellationToken"/> associated with this <see cref="CancellationTokenSource"/>.</summary>
        /// <value>The <see cref="CancellationToken"/> associated with this <see cref="CancellationTokenSource"/>.</value>
        /// <exception cref="ObjectDisposedException">The token source has been disposed.</exception>
        public CancellationToken Token
        {
            get
            {
                ThrowIfDisposed();
                return new CancellationToken(this);
            }
        }

        internal WaitHandle WaitHandle
        {
            get
            {
                ThrowIfDisposed();

                // Return the handle if it was already allocated.
                if (_kernelEvent != null)
                {
                    return _kernelEvent;
                }

                // Lazily-initialize the handle.
                var mre = new ManualResetEvent(false);
                if (Interlocked.CompareExchange(ref _kernelEvent, mre, null) != null)
                {
                    ((IDisposable)mre).Dispose();
                }

                // There is a race condition between checking IsCancellationRequested and setting the event.
                // However, at this point, the kernel object definitely exists and the cases are:
                //   1. if IsCancellationRequested = true, then we will call Set()
                //   2. if IsCancellationRequested = false, then NotifyCancellation will see that the event exists, and will call Set().
                if (IsCancellationRequested)
                {
                    _kernelEvent.Set();
                }

                return _kernelEvent;
            }
        }

        private const long MaxSupportedTimeout = uint.MaxValue - 8;

        /// <summary>Initializes the <see cref="CancellationTokenSource"/>.</summary>
        public CancellationTokenSource() { }

        /// <summary>
        /// Constructs a <see cref="CancellationTokenSource"/> that will be canceled after a specified time span.
        /// </summary>
        /// <param name="delay">The time span to wait before canceling this <see cref="CancellationTokenSource"/></param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The <paramref name="delay"/> is less than -1 or greater than the maximum allowed timer duration.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The countdown for the delay starts during the call to the constructor.  When the delay expires,
        /// the constructed <see cref="CancellationTokenSource"/> is canceled, if it has
        /// not been canceled already.
        /// </para>
        /// <para>
        /// Subsequent calls to CancelAfter will reset the delay for the constructed
        /// <see cref="CancellationTokenSource"/>, if it has not been
        /// canceled already.
        /// </para>
        /// </remarks>
        internal CancellationTokenSource(TimeSpan delay)
        {
            long totalMilliseconds = (long)delay.TotalMilliseconds;
            if (totalMilliseconds < -1 || totalMilliseconds > MaxSupportedTimeout)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.delay);
            }

            InitializeWithTimer(delay);
        }

        /// <summary>
        /// Constructs a <see cref="CancellationTokenSource"/> that will be canceled after a specified time span.
        /// </summary>
        /// <param name="millisecondsDelay">The time span to wait before canceling this <see cref="CancellationTokenSource"/></param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The exception that is thrown when <paramref name="millisecondsDelay"/> is less than -1.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The countdown for the millisecondsDelay starts during the call to the constructor.  When the millisecondsDelay expires,
        /// the constructed <see cref="CancellationTokenSource"/> is canceled (if it has
        /// not been canceled already).
        /// </para>
        /// <para>
        /// Subsequent calls to CancelAfter will reset the millisecondsDelay for the constructed
        /// <see cref="CancellationTokenSource"/>, if it has not been
        /// canceled already.
        /// </para>
        /// </remarks>
        internal CancellationTokenSource(int millisecondsDelay)
        {
            if (millisecondsDelay < -1)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.millisecondsDelay);
            }

            InitializeWithTimer(TimeSpan.FromMilliseconds(millisecondsDelay));
        }

        /// <summary>
        /// Common initialization logic when constructing a CTS with a delay parameter.
        /// A zero delay will result in immediate cancellation.
        /// </summary>
        private void InitializeWithTimer(TimeSpan millisecondsDelay)
        {
            if (millisecondsDelay == TimeSpan.Zero)
            {
                _state = (int)States.NotifyingCompleteState;
            }
            else
            {
                //if (timeProvider == TimeProvider.System)
                //{
                //    _timer = new TimerQueueTimer(s_timerCallback, this, millisecondsDelay, Timeout.InfiniteTimeSpan, flowExecutionContext: false);
                //}
                //else
                //{
                //    using (ExecutionContext.SuppressFlow())
                //    {
                //        _timer = timeProvider.CreateTimer(s_timerCallback, this, millisecondsDelay, Timeout.InfiniteTimeSpan);
                //    }
                //}

                using (ExecutionContext.SuppressFlow())
                {
                    _timer = new Timer(s_timerCallback, this, millisecondsDelay, default);
                }

                // The timer roots this CTS instance while it's scheduled.  That is by design, so
                // that code like:
                //     new CancellationTokenSource(timeout).Token.Register(() => ...);
                // will successfully invoke the delegate after the timeout.
            }
        }

        /// <summary>Communicates a request for cancellation.</summary>
        /// <remarks>
        /// <para>
        /// The associated <see cref="CancellationToken" /> will be notified of the cancellation
        /// and will transition to a state where <see cref="CancellationToken.IsCancellationRequested"/> returns true.
        /// Any callbacks or cancelable operations registered with the <see cref="CancellationToken"/>  will be executed.
        /// </para>
        /// <para>
        /// Cancelable operations and callbacks registered with the token should not throw exceptions.
        /// However, this overload of Cancel will aggregate any exceptions thrown into a <see cref="AggregateException"/>,
        /// such that one callback throwing an exception will not prevent other registered callbacks from being executed.
        /// </para>
        /// <para>
        /// The <see cref="ExecutionContext"/> that was captured when each callback was registered
        /// will be reestablished when the callback is invoked.
        /// </para>
        /// </remarks>
        /// <exception cref="AggregateException">An aggregate exception containing all the exceptions thrown
        /// by the registered callbacks on the associated <see cref="CancellationToken"/>.</exception>
        /// <exception cref="ObjectDisposedException">This <see cref="CancellationTokenSource"/> has been disposed.</exception>
        public void Cancel() => Cancel(false);

        /// <summary>Communicates a request for cancellation.</summary>
        /// <remarks>
        /// <para>
        /// The associated <see cref="CancellationToken" /> will be notified of the cancellation and will transition to a state where
        /// <see cref="CancellationToken.IsCancellationRequested"/> returns true. Any callbacks or cancelable operations registered
        /// with the <see cref="CancellationToken"/>  will be executed.
        /// </para>
        /// <para>
        /// Cancelable operations and callbacks registered with the token should not throw exceptions.
        /// If <paramref name="throwOnFirstException"/> is true, an exception will immediately propagate out of the
        /// call to Cancel, preventing the remaining callbacks and cancelable operations from being processed.
        /// If <paramref name="throwOnFirstException"/> is false, this overload will aggregate any
        /// exceptions thrown into a <see cref="AggregateException"/>,
        /// such that one callback throwing an exception will not prevent other registered callbacks from being executed.
        /// </para>
        /// <para>
        /// The <see cref="ExecutionContext"/> that was captured when each callback was registered
        /// will be reestablished when the callback is invoked.
        /// </para>
        /// </remarks>
        /// <param name="throwOnFirstException">Specifies whether exceptions should immediately propagate.</param>
        /// <exception cref="AggregateException">An aggregate exception containing all the exceptions thrown
        /// by the registered callbacks on the associated <see cref="CancellationToken"/>.</exception>
        /// <exception cref="ObjectDisposedException">This <see cref="CancellationTokenSource"/> has been disposed.</exception>
        public void Cancel(bool throwOnFirstException)
        {
            ThrowIfDisposed();
            NotifyCancellation(throwOnFirstException);
        }

        /*
        /// <summary>Communicates a request for cancellation asynchronously.</summary>
        /// <returns>
        /// A task that will complete after cancelable operations and callbacks registered with the associated
        /// <see cref="CancellationToken" /> have completed.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The associated <see cref="CancellationToken" /> will be notified of the cancellation
        /// and will synchronously transition to a state where <see cref="CancellationToken.IsCancellationRequested"/> returns true.
        /// Any callbacks or cancelable operations registered with the <see cref="CancellationToken"/>  will be executed asynchronously,
        /// with the returned <see cref="Task"/> representing their eventual completion.
        /// </para>
        /// <para>
        /// Callbacks registered with the token should not throw exceptions.
        /// However, any such exceptions that are thrown will be aggregated into an <see cref="AggregateException"/>,
        /// such that one callback throwing an exception will not prevent other registered callbacks from being executed.
        /// </para>
        /// <para>
        /// The <see cref="ExecutionContext"/> that was captured when each callback was registered
        /// will be reestablished when the callback is invoked.
        /// </para>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">This <see cref="CancellationTokenSource"/> has been disposed.</exception>
        public Task CancelAsync()
        {
            if (_disposed)
            {
                return Task.FromException(new ObjectDisposedException(GetType().FullName, SR.CancellationTokenSource_Disposed));
            }

            // Ensure IsCancellationRequested is synchronously transitioned as part of the synchronous call to CancelAsync.
            if (TransitionToCancellationRequested())
            {
                // IsCancellationRequested is now true, and it's used to coordinate with concurrent registrations.
                // Register checks IsCancellationRequested both before and after it adds a node to the registrations
                // list.  If it's true before, it doesn't add the node at all.  If it's true after, it then tries
                // to remove the node from the list; if it's successful in doing so, it invokes the callback itself,
                // and if it's not successful in doing so due to it concurrently being executed, it returns a CTR
                // that enables that concurrently executing callback to be waited on.  As a result of this, now that
                // IsCancellationRequested is true, we can check (under lock) that the callbacks list is null. If it
                // is, there's no work to be done and we can avoid spinning up a task to invoke the callbacks.  It's
                // possible a registration is concurrently happening, but because we're checking the registrations list
                // after setting IsCancellationRequested, either we see a registration that's already registered (in
                // which case we won't short-circuit) or the concurrent registration sees IsCancellationRequested as
                // true after it's registered, in which case it'll go down the unregistering path.
                Debug.Assert(IsCancellationRequested);

                Registrations? registrations = Volatile.Read(ref _registrations);
                if (registrations is not null)
                {
                    registrations.EnterLock();
                    bool callbacksExisted = registrations.Callbacks is not null;
                    registrations.ExitLock();

                    if (callbacksExisted)
                    {
                        // At least at the time we checked, callback existed, so we queue a work item to invoke
                        // the handlers. It's possible those callbacks will have been unregistered by the time
                        // the work item is invoked... that's fine, it just means we spent a bit of energy we
                        // ultimately didn't have to.  Note we explicitly don't schedule each registration individually
                        // to run concurrently, as there's no guarantee they're independent and safe to do so.
                        return Task.Factory.StartNew(s =>
                        {
                            ((CancellationTokenSource)s!).ExecuteCallbackHandlers(throwOnFirstException: false);
                            Debug.Assert(IsCancellationCompleted, "Expected cancellation to have finished");
                        }, this, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                    }
                }
            }

            // As with Cancel, CancelAsync returns immediately if cancellation has already been requested
            // even if cancellation isn't already completed.
            return Task.CompletedTask;
        }
        */

        /// <summary>Schedules a Cancel operation on this <see cref="CancellationTokenSource"/>.</summary>
        /// <param name="delay">The time span to wait before canceling this <see cref="CancellationTokenSource"/>.
        /// </param>
        /// <exception cref="ObjectDisposedException">The exception thrown when this <see
        /// cref="CancellationTokenSource"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The <paramref name="delay"/> is less than -1 or greater than maximum allowed timer duration.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The countdown for the delay starts during this call.  When the delay expires,
        /// this <see cref="CancellationTokenSource"/> is canceled, if it has
        /// not been canceled already.
        /// </para>
        /// <para>
        /// Subsequent calls to CancelAfter will reset the delay for this
        /// <see cref="CancellationTokenSource"/>, if it has not been canceled already.
        /// </para>
        /// </remarks>
        internal void CancelAfter(TimeSpan delay)
        {
            long totalMilliseconds = (long)delay.TotalMilliseconds;
            if (totalMilliseconds < -1 || totalMilliseconds > MaxSupportedTimeout)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.delay);
            }

            CancelAfter((uint)totalMilliseconds);
        }

        /// <summary>
        /// Schedules a Cancel operation on this <see cref="CancellationTokenSource"/>.
        /// </summary>
        /// <param name="millisecondsDelay">The time span to wait before canceling this <see
        /// cref="CancellationTokenSource"/>.
        /// </param>
        /// <exception cref="ObjectDisposedException">The exception thrown when this <see
        /// cref="CancellationTokenSource"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The exception thrown when <paramref name="millisecondsDelay"/> is less than -1.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The countdown for the millisecondsDelay starts during this call.  When the millisecondsDelay expires,
        /// this <see cref="CancellationTokenSource"/> is canceled, if it has
        /// not been canceled already.
        /// </para>
        /// <para>
        /// Subsequent calls to CancelAfter will reset the millisecondsDelay for this
        /// <see cref="CancellationTokenSource"/>, if it has not been
        /// canceled already.
        /// </para>
        /// </remarks>
        internal void CancelAfter(int millisecondsDelay)
        {
            if (millisecondsDelay < -1)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.millisecondsDelay);
            }

            CancelAfter((uint)millisecondsDelay);
        }

        private void CancelAfter(uint millisecondsDelay)
        {
            ThrowIfDisposed();

            if (IsCancellationRequested)
            {
                return;
            }

            // There is a race condition here as a Cancel could occur between the check of
            // IsCancellationRequested and the creation of the timer.  This is benign; in the
            // worst case, a timer will be created that has no effect when it expires.

            // Also, if Dispose() is called right here (after ThrowIfDisposed(), before timer
            // creation), it would result in a leaked Timer object (at least until the timer
            // expired and Disposed itself).  But this would be considered bad behavior, as
            // Dispose() is not thread-safe and should not be called concurrently with CancelAfter().

            Timer? timer = _timer;
            if (timer == null)
            {
                // Lazily initialize the timer in a thread-safe fashion.
                // Initially set to "never go off" because we don't want to take a
                // chance on a timer "losing" the initialization and then
                // cancelling the token before it (the timer) can be disposed.
                using (ExecutionContext.SuppressFlow())
                {
                    timer = new Timer(s_timerCallback, this, TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
                }
                Timer? currentTimer = Interlocked.CompareExchange(ref _timer, timer, null);
                if (currentTimer != null)
                {
                    // We did not initialize the timer.  Dispose the new timer.
                    timer.Dispose();
                    timer = currentTimer;
                }
            }

            timer.Change(
                millisecondsDelay == uint.MaxValue ? TimeSpan.FromMilliseconds(-1) : TimeSpan.FromMilliseconds(millisecondsDelay),
                TimeSpan.FromMilliseconds(-1));
        }

        /// <summary>
        /// Attempts to reset the <see cref="CancellationTokenSource"/> to be used for an unrelated operation.
        /// </summary>
        /// <returns>
        /// true if the <see cref="CancellationTokenSource"/> has not had cancellation requested and could
        /// have its state reset to be reused for a subsequent operation; otherwise, false.
        /// </returns>
        /// <remarks>
        /// <see cref="TryReset"/> is intended to be used by the sole owner of the <see cref="CancellationTokenSource"/>
        /// when it is known that the operation with which the <see cref="CancellationTokenSource"/> was used has
        /// completed, no one else will be attempting to cancel it, and any registrations still remaining are erroneous.
        /// Upon a successful reset, such registrations will no longer be notified for any subsequent cancellation of the
        /// <see cref="CancellationTokenSource"/>; however, if any component still holds a reference to this
        /// <see cref="CancellationTokenSource"/> either directly or indirectly via a <see cref="CancellationToken"/>
        /// handed out from it, polling via their reference will show the current state any time after the reset as
        /// it's the same instance.  Usage of <see cref="TryReset"/> concurrently with requesting cancellation is not
        /// thread-safe and may result in TryReset returning true even if cancellation was already requested and may result
        /// in registrations not being invoked as part of the concurrent cancellation request.
        /// </remarks>
        internal bool TryReset()
        {
            ThrowIfDisposed();

            // We can only reset if cancellation has not yet been requested: we never want to allow a CancellationToken
            // to transition from canceled to non-canceled.
            if (_state == (int)States.NotCanceledState)
            {
                // If there is no timer, then we're free to reset.  If there is a timer, then we need to first try
                // to reset it to be infinite so that it won't fire, and then recognize that it could have already
                // fired by the time we successfully changed it, and so check to see whether that's possibly the case.
                // If we successfully reset it and it never fired, then we can be sure it won't trigger cancellation.
                bool reset = _timer is null;

                if (reset)
                {
                    // We're not canceled and no timer will run to cancel us.
                    // Clear out all the registrations, and return that we've successfully reset.
                    Volatile.Read(ref _registrations)?.UnregisterAll();
                    return true;
                }
            }

            // Failed to reset.
            return false;
        }
        // NOTE: ^^ added in .NET 6

        /// <summary>Releases the resources used by this <see cref="CancellationTokenSource" />.</summary>
        /// <remarks>This method is not thread-safe for any other concurrent calls.</remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="CancellationTokenSource" /> class and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                // We specifically tolerate that a callback can be unregistered
                // after the CTS has been disposed and/or concurrently with cts.Dispose().
                // This is safe without locks because Dispose doesn't interact with values
                // in the callback partitions, only nulling out the ref to existing partitions.
                //
                // We also tolerate that a callback can be registered after the CTS has been
                // disposed.  This is safe because InternalRegister is tolerant
                // of _callbackPartitions becoming null during its execution.  However,
                // we run the acceptable risk of _callbackPartitions getting reinitialized
                // to non-null if there is a race between Dispose and Register, in which case this
                // instance may unnecessarily hold onto a registered callback.  But that's no worse
                // than if Dispose wasn't safe to use concurrently, as Dispose would never be called,
                // and thus no handlers would be dropped.
                //
                // And, we tolerate Dispose being used concurrently with Cancel.  This is necessary
                // to properly support, e.g., LinkedCancellationTokenSource, where, due to common usage patterns,
                // it's possible for this pairing to occur with valid usage (e.g. a component accepts
                // an external CancellationToken and uses CreateLinkedTokenSource to combine it with an
                // internal source of cancellation, then Disposes of that linked source, which could
                // happen at the same time the external entity is requesting cancellation).

                Timer? timer = _timer;
                if (timer != null)
                {
                    _timer = null;
                    timer.Dispose(); // ITimer.Dispose is thread-safe
                }

                _registrations = null; // allow the GC to clean up registrations

                // If a kernel event was created via WaitHandle, we'd like to Dispose of it.  However,
                // we only want to do so if it's not being used by Cancel concurrently.  First, we
                // interlocked exchange it to be null, and then we check whether cancellation is currently
                // in progress.  NotifyCancellation will only try to set the event if it exists after it's
                // transitioned to and while it's in the NotifyingState.
                if (_kernelEvent != null)
                {
                    ManualResetEvent? mre = Interlocked.Exchange<ManualResetEvent?>(ref _kernelEvent!, null);
                    if (mre != null && _state != (int)States.NotifyingState)
                    {
                        ((IDisposable)mre).Dispose();
                    }
                }

                _disposed = true;
            }
        }

        /// <summary>Throws an exception if the source has been disposed.</summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                ThrowHelper.ThrowObjectDisposedException(this);
            }
        }

        /// <summary>
        /// Registers a callback object. If cancellation has already occurred, the
        /// callback will have been run by the time this method returns.
        /// </summary>
        internal CancellationTokenRegistration Register(
            Delegate callback, object? stateForCallback, SynchronizationContext? syncContext, ExecutionContext? executionContext)
        {
            Debug.Assert(this != s_neverCanceledSource, "This source should never be exposed via a CancellationToken.");
            Debug.Assert(callback is Action<object?> || callback is Action<object?, CancellationToken>);

            // If not canceled, register the handler; if canceled already, run the callback synchronously.
            if (!IsCancellationRequested)
            {
                // We allow Dispose to be called concurrently with Register.  While this is not a recommended practice,
                // consumers can and do use it this way.
                if (_disposed)
                {
                    return default;
                }

                // Get the registrations object. It's lazily initialized to keep the size of a CTS smaller for situations
                // where all operations associated with the CTS complete synchronously and never actually need to register,
                // or all only poll.
                Registrations? registrations = Volatile.Read(ref _registrations);
                if (registrations is null)
                {
                    registrations = new Registrations(this);
                    registrations = Interlocked.CompareExchange(ref _registrations, registrations, null) ?? registrations;
                }

                // If it looks like there's a node in the freelist we could grab, grab the lock and try to get, configure,
                // and register the node.
                CallbackNode? node = null;
                long id = 0;
                if (registrations.FreeNodeList is not null)
                {
                    registrations.EnterLock();
                    try
                    {
                        // Try to take a free node.  If we're able to, configure the node and register it.
                        node = registrations.FreeNodeList;
                        if (node is not null)
                        {
                            Debug.Assert(node.Prev == null, "Nodes in the free list should all have a null Prev");
                            registrations.FreeNodeList = node.Next;

                            node.Id = id = registrations.NextAvailableId++;
                            node.Callback = callback;
                            node.CallbackState = stateForCallback;
                            node.ExecutionContext = executionContext;
                            node.SynchronizationContext = syncContext;
                            node.Next = registrations.Callbacks;
                            registrations.Callbacks = node;
                            if (node.Next != null)
                            {
                                node.Next.Prev = node;
                            }
                        }
                    }
                    finally
                    {
                        registrations.ExitLock();
                    }
                }

                // If we were unsuccessful in using a free node, create a new one, configure it, and register it.
                if (node is null)
                {
                    // Allocate the node if we couldn't get one from the free list.  We avoid
                    // doing this while holding the spin lock, to avoid a potentially arbitrary
                    // amount of GC-related work under the lock, which we aim to keep very tight,
                    // just a few assignments.
                    node = new CallbackNode(registrations);

                    node.Callback = callback;
                    node.CallbackState = stateForCallback;
                    node.ExecutionContext = executionContext;
                    node.SynchronizationContext = syncContext;

                    registrations.EnterLock();
                    try
                    {
                        node.Id = id = registrations.NextAvailableId++;
                        node.Next = registrations.Callbacks;
                        if (node.Next != null)
                        {
                            node.Next.Prev = node;
                        }
                        registrations.Callbacks = node;
                    }
                    finally
                    {
                        registrations.ExitLock();
                    }
                }

                // If cancellation hasn't been requested, return the registration.
                // if cancellation has been requested, try to undo the registration and run the callback
                // ourselves, but if we can't unregister it (e.g. the thread running Cancel snagged
                // our callback for execution), return the registration so that the caller can wait
                // for callback completion in ctr.Dispose().
                Debug.Assert(id != 0, "IDs should never be the reserved value 0.");
                if (!IsCancellationRequested || !registrations.Unregister(id, node))
                {
                    return new CancellationTokenRegistration(id, node);
                }
            }

            // Cancellation already occurred.  Run the callback on this thread and return an empty registration.
            Invoke(callback, stateForCallback, this);
            return default;
        }

        private void NotifyCancellation(bool throwOnFirstException)
        {
            if (TransitionToCancellationRequested())
            {
                // If we're the first to signal cancellation, do the main extra work.
                ExecuteCallbackHandlers(throwOnFirstException);
                Debug.Assert(IsCancellationCompleted, "Expected cancellation to have finished");
            }
        }

        /// <summary>Transitions from <see cref="States.NotCanceledState"/> to <see cref="States.NotifyingState"/>.</summary>
        /// <returns>true if it successfully transitioned; otherwise, false.</returns>
        /// <remarks>If it successfully transitions, it will also have disposed of <see cref="_timer"/> and set <see cref="_kernelEvent"/>.</remarks>
        private bool TransitionToCancellationRequested()
        {
            if (!IsCancellationRequested &&
                Interlocked.CompareExchange(ref _state, (int)States.NotifyingState, (int)States.NotCanceledState) == (int)States.NotCanceledState)
            {
                // Dispose of the timer, if any.  Dispose may be running concurrently here, but ITimer.Dispose is thread-safe.
                Timer? timer = _timer;
                if (timer != null)
                {
                    _timer = null;
                    timer.Dispose();
                }

                // Set the event if it's been lazily initialized and hasn't yet been disposed of.  Dispose may
                // be running concurrently, in which case either it'll have set m_kernelEvent back to null and
                // we won't see it here, or it'll see that we've transitioned to NOTIFYING and will skip disposing it,
                // leaving cleanup to finalization.
                _kernelEvent?.Set();

                return true;
            }

            return false;
        }

        /// <summary>Invoke all registered callbacks.</summary>
        /// <remarks>The handlers are invoked synchronously in LIFO order.</remarks>
        private void ExecuteCallbackHandlers(bool throwOnFirstException)
        {
            Debug.Assert(IsCancellationRequested, "ExecuteCallbackHandlers should only be called after setting IsCancellationRequested->true");

            // If there are no callbacks to run, we can safely exit.  Any race conditions to lazy initialize it
            // will see IsCancellationRequested and will then run the callback themselves.
            Registrations? registrations = Interlocked.Exchange(ref _registrations, null);
            if (registrations is null)
            {
                Interlocked.Exchange(ref _state, (int)States.NotifyingCompleteState);
                return;
            }

            // Record the threadID being used for running the callbacks.
            registrations.ThreadIDExecutingCallbacks = Environment.CurrentManagedThreadId;

            List<Exception>? exceptionList = null;
            try
            {
                // We call the delegates in LIFO order on each partition so that callbacks fire 'deepest first'.
                // This is intended to help with nesting scenarios so that child enlisters cancel before their parents.

                // Iterate through all nodes in the partition.  We remove each node prior
                // to processing it.  This allows for unregistration of subsequent registrations
                // to still be effective even as other registrations are being invoked.
                while (true)
                {
                    CallbackNode? node;
                    registrations.EnterLock();
                    try
                    {
                        // Pop the next registration from the callbacks list.
                        node = registrations.Callbacks;
                        if (node == null)
                        {
                            // No more registrations to process.
                            break;
                        }

                        Debug.Assert(node.Registrations.Source == this);
                        Debug.Assert(node.Prev == null);
                        if (node.Next != null)
                        {
                            node.Next.Prev = null;
                        }
                        registrations.Callbacks = node.Next;

                        // Publish the intended callback ID, to ensure ctr.Dispose can tell if a wait is necessary.
                        // This write happens while the lock is held so that Dispose is either able to successfully
                        // unregister or is guaranteed to see an accurate executing callback ID, since it takes
                        // the same lock to remove the node from the callback list.
                        registrations.ExecutingCallbackId = node.Id;

                        // Now that we've grabbed the Id, reset the node's Id to 0.  This signals
                        // to code unregistering that the node is no longer associated with a callback.
                        node.Id = 0;
                    }
                    finally
                    {
                        registrations.ExitLock();
                    }

                    // Invoke the callback on this thread if there's no sync context or on the
                    // target sync context if there is one.
                    try
                    {
                        if (node.SynchronizationContext != null)
                        {
                            // Transition to the target syncContext and continue there.
                            node.SynchronizationContext.Send(static s =>
                            {
                                var n = (CallbackNode)s!;
                                n.Registrations.ThreadIDExecutingCallbacks = Environment.CurrentManagedThreadId;
                                n.ExecuteCallback();
                            }, node);
                            registrations.ThreadIDExecutingCallbacks = Environment.CurrentManagedThreadId; // above may have altered ThreadIDExecutingCallbacks, so reset it
                        }
                        else
                        {
                            node.ExecuteCallback();
                        }
                    }
                    catch (Exception ex) when (!throwOnFirstException)
                    {
                        // Store the exception and continue
                        (exceptionList ??= new List<Exception>()).Add(ex);
                    }

                    // Drop the node. While we could add it to the free list, doing so has cost (we'd need to take the lock again)
                    // and very limited value.  Since a source can only be canceled once, and after it's canceled registrations don't
                    // need nodes, the only benefit to putting this on the free list would be if Register raced with cancellation
                    // occurring, such that it could have used this free node but would instead need to allocate a new node (if
                    // there wasn't another free node available).
                }
            }
            finally
            {
                _state = (int)States.NotifyingCompleteState;
                Interlocked.Exchange(ref registrations.ExecutingCallbackId, 0); // for safety, prevent reorderings crossing this point and seeing inconsistent state.
            }

            if (exceptionList != null)
            {
                Debug.Assert(exceptionList.Count > 0, $"Expected {exceptionList.Count} > 0");
                throw new AggregateException(exceptionList);
            }
        }

        /// <summary>
        /// Creates a <see cref="CancellationTokenSource"/> that will be in the canceled state
        /// when any of the source tokens are in the canceled state.
        /// </summary>
        /// <param name="token1">The first <see cref="CancellationToken">CancellationToken</see> to observe.</param>
        /// <param name="token2">The second <see cref="CancellationToken">CancellationToken</see> to observe.</param>
        /// <returns>A <see cref="CancellationTokenSource"/> that is linked
        /// to the source tokens.</returns>
        public static CancellationTokenSource CreateLinkedTokenSource(CancellationToken token1, CancellationToken token2) =>
            !token1.CanBeCanceled ? CreateLinkedTokenSource(token2) :
            token2.CanBeCanceled ? new Linked2CancellationTokenSource(token1, token2) :
            (CancellationTokenSource)new Linked1CancellationTokenSource(token1);

        /// <summary>
        /// Creates a <see cref="CancellationTokenSource"/> that will be in the canceled state
        /// when the supplied token is in the canceled state.
        /// </summary>
        /// <param name="token">The <see cref="CancellationToken">CancellationToken</see> to observe.</param>
        /// <returns>A <see cref="CancellationTokenSource"/> that is linked to the source token.</returns>
        internal static CancellationTokenSource CreateLinkedTokenSource(CancellationToken token) =>
            token.CanBeCanceled ? new Linked1CancellationTokenSource(token) : new CancellationTokenSource();

        /// <summary>
        /// Creates a <see cref="CancellationTokenSource"/> that will be in the canceled state
        /// when any of the source tokens are in the canceled state.
        /// </summary>
        /// <param name="tokens">The <see cref="CancellationToken">CancellationToken</see> instances to observe.</param>
        /// <returns>A <see cref="CancellationTokenSource"/> that is linked to the source tokens.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tokens"/> is null.</exception>
        public static CancellationTokenSource CreateLinkedTokenSource(params CancellationToken[] tokens)
        {
            ArgumentNullException.ThrowIfNull(tokens);
            return CreateLinkedTokenSource((ReadOnlySpan<CancellationToken>)tokens);
        }

        /// <summary>
        /// Creates a <see cref="CancellationTokenSource"/> that will be in the canceled state
        /// when any of the source tokens are in the canceled state.
        /// </summary>
        /// <param name="tokens">The <see cref="CancellationToken">CancellationToken</see> instances to observe.</param>
        /// <returns>A <see cref="CancellationTokenSource"/> that is linked to the source tokens.</returns>
        internal static CancellationTokenSource CreateLinkedTokenSource(params ReadOnlySpan<CancellationToken> tokens)
        {
            return tokens.Length switch
            {
                0 => throw new ArgumentException("CancellationToken_CreateLinkedToken_TokensIsEmpty"),
                1 => CreateLinkedTokenSource(tokens[0]),
                2 => CreateLinkedTokenSource(tokens[0], tokens[1]),

                // a defensive copy is not required as the array has value-items that have only a single reference field,
                // hence each item cannot be null itself, and reads of the payloads cannot be torn.
                _ => new LinkedNCancellationTokenSource(tokens),
            };
        }

        private sealed class Linked1CancellationTokenSource : CancellationTokenSource
        {
            private readonly CancellationTokenRegistration _reg1;

            internal Linked1CancellationTokenSource(CancellationToken token1)
            {
                _reg1 = token1.UnsafeRegister(LinkedNCancellationTokenSource.s_linkedTokenCancelDelegate, this);
            }

            protected override void Dispose(bool disposing)
            {
                if (!disposing || _disposed)
                {
                    return;
                }

                _reg1.Dispose();
                base.Dispose(disposing);
            }
        }

        private sealed class Linked2CancellationTokenSource : CancellationTokenSource
        {
            private readonly CancellationTokenRegistration _reg1;
            private readonly CancellationTokenRegistration _reg2;

            internal Linked2CancellationTokenSource(CancellationToken token1, CancellationToken token2)
            {
                _reg1 = token1.UnsafeRegister(LinkedNCancellationTokenSource.s_linkedTokenCancelDelegate, this);
                _reg2 = token2.UnsafeRegister(LinkedNCancellationTokenSource.s_linkedTokenCancelDelegate, this);
            }

            protected override void Dispose(bool disposing)
            {
                if (!disposing || _disposed)
                {
                    return;
                }

                _reg1.Dispose();
                _reg2.Dispose();
                base.Dispose(disposing);
            }
        }

        private sealed class LinkedNCancellationTokenSource : CancellationTokenSource
        {
            internal static readonly Action<object?> s_linkedTokenCancelDelegate = static s =>
            {
                Debug.Assert(s is CancellationTokenSource, $"Expected {typeof(CancellationTokenSource)}, got {s}");
                ((CancellationTokenSource)s!).NotifyCancellation(throwOnFirstException: false); // skip ThrowIfDisposed() check in Cancel()
            };
            private CancellationTokenRegistration[]? _linkingRegistrations;

            internal LinkedNCancellationTokenSource(ReadOnlySpan<CancellationToken> tokens)
            {
                _linkingRegistrations = new CancellationTokenRegistration[tokens.Length];

                for (int i = 0; i < tokens.Length; i++)
                {
                    if (tokens[i].CanBeCanceled)
                    {
                        _linkingRegistrations[i] = tokens[i].UnsafeRegister(s_linkedTokenCancelDelegate, this);
                    }
                    // Empty slots in the array will be default(CancellationTokenRegistration), which are nops to Dispose.
                    // Based on usage patterns, such occurrences should also be rare, such that it's not worth resizing
                    // the array and incurring the related costs.
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (!disposing || _disposed)
                {
                    return;
                }

                CancellationTokenRegistration[]? linkingRegistrations = _linkingRegistrations;
                if (linkingRegistrations != null)
                {
                    _linkingRegistrations = null; // release for GC once we're done enumerating
                    for (int i = 0; i < linkingRegistrations.Length; i++)
                    {
                        linkingRegistrations[i].Dispose();
                    }
                }

                base.Dispose(disposing);
            }
        }

        private static void Invoke(Delegate d, object? state, CancellationTokenSource source)
        {
            Debug.Assert(d is Action<object?> || d is Action<object?, CancellationToken>);

            if (d is Action<object?> actionWithState)
            {
                actionWithState(state);
            }
            else
            {
                ((Action<object?, CancellationToken>)d)(state, new CancellationToken(source));
            }
        }

        /// <summary>Set of all the registrations in the token source.</summary>
        /// <remarks>
        /// Separated out into a separate instance to keep CancellationTokenSource smaller for the case where one is created but nothing is registered with it.
        /// This happens not infrequently, in particular when one is created for an operation that ends up completing synchronously / quickly.
        /// </remarks>
        internal sealed class Registrations
        {
            /// <summary>The associated source.</summary>
            public readonly CancellationTokenSource Source;
            /// <summary>Doubly-linked list of callbacks registered with the source. Callbacks are removed during unregistration and as they're invoked.</summary>
            public CallbackNode? Callbacks;
            /// <summary>Singly-linked list of free nodes that can be used for subsequent callback registrations.</summary>
            public CallbackNode? FreeNodeList;
            /// <summary>Every callback is assigned a unique, never-reused ID.  This defines the next available ID.</summary>
            public long NextAvailableId = 1; // avoid using 0, as that's the default long value and used to represent an empty node
            /// <summary>Tracks the running callback to assist ctr.Dispose() to wait for the target callback to complete.</summary>
            public long ExecutingCallbackId;
            /// <summary>The ID of the thread currently executing the main body of CTS.Cancel()</summary>
            /// <remarks>
            /// This helps us to know if a call to ctr.Dispose() is running 'within' a cancellation callback.
            /// This is updated as we move between the main thread calling cts.Cancel() and any syncContexts
            /// that are used to actually run the callbacks.
            /// </remarks>
            public volatile int ThreadIDExecutingCallbacks = -1;
            /// <summary>Spin lock that protects state in the instance.</summary>
            private volatile int _locked;

            /// <summary>Initializes the instance.</summary>
            /// <param name="source">The associated source.</param>
            public Registrations(CancellationTokenSource source) => Source = source;

            [MethodImpl(MethodImplOptions.AggressiveInlining)] // used in only two places, one of which is a hot path
            private void Recycle(CallbackNode node)
            {
                Debug.Assert(_locked != 0);

                // Clear out the unused node and put it on the singly-linked free list.
                // The only field we don't clear out is the associated Registrations, as that's fixed
                // throughout the node's lifetime.
                node.Id = 0;
                node.Callback = null;
                node.CallbackState = null;
                node.ExecutionContext = null;
                node.SynchronizationContext = null;

                node.Prev = null;
                node.Next = FreeNodeList;
                FreeNodeList = node;
            }

            /// <summary>Unregisters a callback.</summary>
            /// <param name="id">The expected id of the registration.</param>
            /// <param name="node">The callback node.</param>
            /// <returns>true if the node was found and removed; false if it couldn't be found or didn't match the provided id.</returns>
            public bool Unregister(long id, CallbackNode node)
            {
                Debug.Assert(node != null, "Expected non-null node");
                Debug.Assert(node!.Registrations == this, "Expected node to come from this registrations instance");

                if (id == 0)
                {
                    // In general, we won't get 0 passed in here.  However, race conditions between threads
                    // Unregistering and also zero'ing out the CancellationTokenRegistration could cause 0
                    // to be passed in here, in which case there's nothing to do. 0 is never a valid id.
                    return false;
                }

                EnterLock();
                try
                {
                    if (node.Id != id)
                    {
                        // Either:
                        // - The callback is currently or has already been invoked, in which case node.Id
                        //   will no longer equal the assigned id, as it will have transitioned to 0.
                        // - The registration was already disposed of, in which case node.Id will similarly
                        //   no longer equal the assigned id, as it will have transitioned to 0 and potentially
                        //   then to another (larger) value when reused for a new registration.
                        // In either case, there's nothing to unregister.
                        return false;
                    }

                    // The registration must still be in the callbacks list.  Remove it.
                    if (Callbacks == node)
                    {
                        Debug.Assert(node.Prev == null);
                        Callbacks = node.Next;
                    }
                    else
                    {
                        Debug.Assert(node.Prev != null);
                        node.Prev!.Next = node.Next;
                    }

                    if (node.Next != null)
                    {
                        node.Next.Prev = node.Prev;
                    }

                    Recycle(node);

                    return true;
                }
                finally
                {
                    ExitLock();
                }
            }

            /// <summary>Moves all registrations to the free list.</summary>
            public void UnregisterAll()
            {
                EnterLock();
                try
                {
                    // Null out all callbacks.
                    CallbackNode? node = Callbacks;
                    Callbacks = null;

                    // Reset and move each node that was in the callbacks list to the free list.
                    while (node != null)
                    {
                        CallbackNode? next = node.Next;
                        Recycle(node);
                        node = next;
                    }
                }
                finally
                {
                    ExitLock();
                }
            }

            /// <summary>
            /// Wait for a single callback to complete (or, more specifically, to not be running).
            /// It is ok to call this method if the callback has already finished.
            /// Calling this method before the target callback has been selected for execution would be an error.
            /// </summary>
            public void WaitForCallbackToComplete(long id)
            {
                SpinWait sw = default;
                while (Volatile.Read(ref ExecutingCallbackId) == id)
                {
                    sw.SpinOnce();  // spin, as we assume callback execution is fast and that this situation is rare.
                }
            }

            /*
            /// <summary>
            /// Asynchronously wait for a single callback to complete (or, more specifically, to not be running).
            /// It is ok to call this method if the callback has already finished.
            /// Calling this method before the target callback has been selected for execution would be an error.
            /// </summary>
            public async ValueTask WaitForCallbackToCompleteAsync(long id)
            {
                // Employ an async loop that'll poll for the currently executing callback to complete. While such polling isn't
                // ideal, we expect this to be a rare case (disposing while the associated callback is running), and brief when
                // it happens (so the polling will be minimal), and making this work with a callback mechanism will add additional
                // cost to other more common cases.
                while (Volatile.Read(ref ExecutingCallbackId) == id)
                {
                    await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
                }
            }
            */

            /// <summary>Enters the lock for this instance.  The current thread must not be holding the lock, but that is not validated.</summary>
            public void EnterLock()
            {
                ref int value = ref _locked;
                if (Interlocked.Exchange(ref value, 1) != 0)
                {
                    Contention(ref value);

                    [MethodImpl(MethodImplOptions.NoInlining)]
                    static void Contention(ref int value)
                    {
                        SpinWait sw = default;
                        do { sw.SpinOnce(); } while (Interlocked.Exchange(ref value, 1) != 0);
                    }
                }
            }

            /// <summary>Exits the lock for this instance.  The current thread must be holding the lock, but that is not validated.</summary>
            public void ExitLock()
            {
                Debug.Assert(_locked != 0);
                _locked = 0;
            }
        }

        /// <summary>All of the state associated a registered callback, in a node that's part of a linked list of registered callbacks.</summary>
        internal sealed class CallbackNode
        {
            public readonly Registrations Registrations;
            public CallbackNode? Prev;
            public CallbackNode? Next;

            public long Id;
            public Delegate? Callback; // Action<object> or Action<object,CancellationToken>
            public object? CallbackState;
            public ExecutionContext? ExecutionContext;
            public SynchronizationContext? SynchronizationContext;

            public CallbackNode(Registrations registrations)
            {
                Debug.Assert(registrations != null, "Expected non-null parent registrations");
                Registrations = registrations!;
            }

            public void ExecuteCallback()
            {
                ExecutionContext? context = ExecutionContext;
                if (context is null)
                {
                    Debug.Assert(Callback != null);
                    Invoke(Callback!, CallbackState, Registrations.Source);
                }
                else
                {
                    ExecutionContext.Run(context, static s =>
                    {
                        var node = (CallbackNode)s!;
                        Debug.Assert(node.Callback != null);
                        Invoke(node.Callback!, node.CallbackState, node.Registrations.Source);
                    }, this);
                }
            }
        }
    }
}
