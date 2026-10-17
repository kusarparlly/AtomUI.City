namespace AtomUI.City.Core.Threading;

/// <summary>
/// Defines the contract for iui dispatcher.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Reports whether the current thread can synchronously access UI-owned state.
    /// Async callers should use InvokeAsync and let the dispatcher choose inline or queued execution.
    /// </summary>
    bool CheckAccess();

    /// <summary>
    /// Invokes a callback at most once on the UI thread and completes after the callback finishes.
    /// Callback failures are propagated through the returned operation.
    /// </summary>
    ValueTask InvokeAsync(Action callback) => InvokeAsync(callback, CancellationToken.None);

    /// <summary>
    /// Invokes a callback at most once on the UI thread and observes cancellation before it starts.
    /// The returned operation completes after the callback finishes and propagates callback failures.
    /// </summary>
    ValueTask InvokeAsync(Action callback, CancellationToken cancellationToken);

    /// <summary>
    /// Invokes a value-returning callback at most once on the UI thread and completes after it finishes.
    /// Callback failures are propagated through the returned operation.
    /// </summary>
    ValueTask<T> InvokeAsync<T>(Func<T> callback) => InvokeAsync(callback, CancellationToken.None);

    /// <summary>
    /// Invokes a value-returning callback at most once on the UI thread and observes cancellation before it starts.
    /// The returned operation completes after the callback finishes and propagates callback failures.
    /// </summary>
    ValueTask<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken);

    /// <summary>
    /// Invokes an asynchronous callback at most once from the UI thread.
    /// The returned operation completes after the callback finishes and propagates callback failures.
    /// </summary>
    ValueTask PostAsync(
        Func<CancellationToken, ValueTask> callback,
        CancellationToken cancellationToken = default);
}
