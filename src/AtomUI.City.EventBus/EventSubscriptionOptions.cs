using AtomUI.City.Core.Threading;

namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event subscription options.
/// </summary>
public sealed class EventSubscriptionOptions
{
    private EventSubscriptionOptions(
        EventDispatchPolicy dispatchPolicy,
        EventDispatchMode dispatchMode,
        IUiDispatcher? uiDispatcher,
        EventErrorPolicy errorPolicy,
        TimeSpan? handlerTimeout,
        int disableSubscriptionAfterFailures)
    {
        DispatchPolicy = dispatchPolicy;
        DispatchMode = dispatchMode;
        UiDispatcher = uiDispatcher;
        ErrorPolicy = errorPolicy;
        HandlerTimeout = ValidateHandlerTimeout(handlerTimeout);
        DisableSubscriptionAfterFailures = ValidateDisableThreshold(disableSubscriptionAfterFailures);
    }

    /// <summary>
    /// Gets serialized.
    /// </summary>
    public static EventSubscriptionOptions Serialized { get; } = new(
        EventDispatchPolicy.Serialized,
        EventDispatchMode.InlineIfAllowed,
        uiDispatcher: null,
        EventErrorPolicy.ContinueAndReport,
        handlerTimeout: TimeSpan.FromSeconds(30),
        disableSubscriptionAfterFailures: 3);

    /// <summary>
    /// Gets current.
    /// </summary>
    public static EventSubscriptionOptions Current { get; } = new(
        EventDispatchPolicy.Current,
        EventDispatchMode.InlineIfAllowed,
        uiDispatcher: null,
        EventErrorPolicy.ContinueAndReport,
        handlerTimeout: TimeSpan.FromSeconds(30),
        disableSubscriptionAfterFailures: 3);

    /// <summary>
    /// Gets dispatch policy.
    /// </summary>
    public EventDispatchPolicy DispatchPolicy { get; }

    /// <summary>
    /// Gets dispatch mode.
    /// </summary>
    public EventDispatchMode DispatchMode { get; }

    /// <summary>
    /// Gets ui dispatcher.
    /// </summary>
    public IUiDispatcher? UiDispatcher { get; }

    /// <summary>
    /// Gets error policy.
    /// </summary>
    public EventErrorPolicy ErrorPolicy { get; }

    /// <summary>
    /// Gets handler timeout.
    /// </summary>
    public TimeSpan? HandlerTimeout { get; }

    /// <summary>
    /// Gets disable subscription after failures.
    /// </summary>
    public int DisableSubscriptionAfterFailures { get; }

    /// <summary>
    /// Executes the ui thread operation.
    /// </summary>
    public static EventSubscriptionOptions UiThread(
        IUiDispatcher dispatcher,
        EventDispatchMode dispatchMode = EventDispatchMode.Post)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ValidateDispatchMode(dispatchMode);

        return new EventSubscriptionOptions(
            EventDispatchPolicy.UiThread,
            dispatchMode,
            dispatcher,
            EventErrorPolicy.ContinueAndReport,
            handlerTimeout: TimeSpan.FromSeconds(30),
            disableSubscriptionAfterFailures: 3);
    }

    /// <summary>
    /// Executes the background operation.
    /// </summary>
    public static EventSubscriptionOptions Background()
    {
        return new EventSubscriptionOptions(
            EventDispatchPolicy.Background,
            EventDispatchMode.Post,
            uiDispatcher: null,
            EventErrorPolicy.ContinueAndReport,
            handlerTimeout: TimeSpan.FromSeconds(30),
            disableSubscriptionAfterFailures: 3);
    }

    /// <summary>
    /// Executes the with error policy operation.
    /// </summary>
    public EventSubscriptionOptions WithErrorPolicy(EventErrorPolicy errorPolicy)
    {
        if (!Enum.IsDefined(errorPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(errorPolicy),
                errorPolicy,
                "Event error policy is not supported.");
        }

        return new EventSubscriptionOptions(
            DispatchPolicy,
            DispatchMode,
            UiDispatcher,
            errorPolicy,
            HandlerTimeout,
            DisableSubscriptionAfterFailures);
    }

    /// <summary>
    /// Executes the with handler timeout operation.
    /// </summary>
    public EventSubscriptionOptions WithHandlerTimeout(TimeSpan? handlerTimeout)
    {
        return new EventSubscriptionOptions(
            DispatchPolicy,
            DispatchMode,
            UiDispatcher,
            ErrorPolicy,
            handlerTimeout,
            DisableSubscriptionAfterFailures);
    }

    /// <summary>
    /// Executes the with disable subscription after failures operation.
    /// </summary>
    public EventSubscriptionOptions WithDisableSubscriptionAfterFailures(int failureCount)
    {
        return new EventSubscriptionOptions(
            DispatchPolicy,
            DispatchMode,
            UiDispatcher,
            ErrorPolicy,
            HandlerTimeout,
            failureCount);
    }

    private static TimeSpan? ValidateHandlerTimeout(TimeSpan? handlerTimeout)
    {
        if (handlerTimeout is { } timeout &&
            (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue))
        {
            throw new ArgumentOutOfRangeException(
                nameof(handlerTimeout),
                handlerTimeout,
                $"Handler timeout must be greater than zero and no greater than {int.MaxValue} milliseconds.");
        }

        return handlerTimeout;
    }

    private static int ValidateDisableThreshold(int failureCount)
    {
        if (failureCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureCount),
                failureCount,
                "Disable-subscription failure threshold must be greater than zero.");
        }

        return failureCount;
    }

    private static void ValidateDispatchMode(EventDispatchMode dispatchMode)
    {
        if (!Enum.IsDefined(dispatchMode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(dispatchMode),
                dispatchMode,
                "Event dispatch mode is not supported.");
        }
    }
}
