namespace AtomUI.City.Presentation;

internal static class PresentationStateTransitions
{
    public static void Ensure(
        PresentationRuntimeState current,
        PresentationRuntimeState next)
    {
        if (current == next || next == PresentationRuntimeState.Faulted)
        {
            return;
        }

        var allowed = current switch
        {
            PresentationRuntimeState.NotReady => next is PresentationRuntimeState.Ready or PresentationRuntimeState.Stopping,
            PresentationRuntimeState.Ready => next == PresentationRuntimeState.Stopping,
            PresentationRuntimeState.Stopping => next == PresentationRuntimeState.Stopped,
            _ => false,
        };
        ThrowIfInvalid(allowed, current, next);
    }

    public static void Ensure(
        WindowSessionState current,
        WindowSessionState next)
    {
        if (current == next || next == WindowSessionState.Faulted)
        {
            return;
        }

        var allowed = current switch
        {
            WindowSessionState.Registered => next == WindowSessionState.Ready,
            WindowSessionState.Ready => next == WindowSessionState.Closing,
            WindowSessionState.Closing => next is WindowSessionState.Ready or WindowSessionState.Closed,
            _ => false,
        };
        ThrowIfInvalid(allowed, current, next);
    }

    public static void Ensure(
        RouteOutletState current,
        RouteOutletState next)
    {
        if (current == next)
        {
            return;
        }

        var allowed = current switch
        {
            RouteOutletState.Empty => next is RouteOutletState.Preparing or RouteOutletState.Stopping or RouteOutletState.Faulted,
            RouteOutletState.Committed => next is RouteOutletState.Preparing or RouteOutletState.Stopping or RouteOutletState.Faulted,
            RouteOutletState.OutOfSync => next is RouteOutletState.Preparing or RouteOutletState.Stopping or RouteOutletState.Faulted,
            RouteOutletState.Preparing => next is RouteOutletState.Empty or RouteOutletState.TemporaryAttached or
                RouteOutletState.Committed or RouteOutletState.OutOfSync or RouteOutletState.Stopping or RouteOutletState.Faulted,
            RouteOutletState.TemporaryAttached => next is RouteOutletState.Committed or RouteOutletState.OutOfSync or
                RouteOutletState.Stopping or RouteOutletState.Faulted,
            RouteOutletState.Stopping => next is RouteOutletState.Stopped or RouteOutletState.Faulted,
            RouteOutletState.Faulted => next is RouteOutletState.Stopping or RouteOutletState.Stopped,
            _ => false,
        };
        ThrowIfInvalid(allowed, current, next);
    }

    private static void ThrowIfInvalid<TState>(bool allowed, TState current, TState next)
        where TState : struct, Enum
    {
        if (!allowed)
        {
            throw new InvalidOperationException(
                $"Invalid Presentation state transition '{typeof(TState).Name}:{current}->{next}'.");
        }
    }
}
