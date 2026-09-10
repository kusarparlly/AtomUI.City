using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

public interface IInteractionHandlerRegistry
{
    IDisposable Register<TRequest, TResult>(
        Func<InteractionContext<TRequest>, CancellationToken, ValueTask<TResult>> handler,
        IActivationScope? activationScope = null);

    IDisposable Register<TRequest, TResult>(
        Func<InteractionContext<TRequest>, CancellationToken, ValueTask<TResult>> handler,
        InteractionHandlerRegistrationOptions options);

    ValueTask<InteractionResult<TResult>> HandleAsync<TRequest, TResult>(
        TRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<InteractionResult<TResult>> HandleAsync<TRequest, TResult>(
        TRequest request,
        InteractionDispatchContext context,
        CancellationToken cancellationToken = default);

    int RevokePlugin(string pluginId);

    int RevokeContribution(string contributionId);

    PresentationQueueSnapshot GetModalQueueSnapshot(string? windowId = null) => new(
        PresentationQueueOptions.DefaultModalInteractionPendingCapacity,
        PendingCount: 0,
        InFlightCount: 0,
        PeakPendingCount: 0,
        RejectedCount: 0);
}
