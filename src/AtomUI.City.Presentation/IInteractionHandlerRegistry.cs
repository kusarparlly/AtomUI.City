using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

/// <summary>
/// Defines the contract for iinteraction handler registry.
/// </summary>
public interface IInteractionHandlerRegistry
{
    /// <summary>
    /// Executes the register&lt;trequest, tresult&gt; operation.
    /// </summary>
    IDisposable Register<TRequest, TResult>(
        Func<InteractionContext<TRequest>, CancellationToken, ValueTask<TResult>> handler,
        IActivationScope? activationScope = null);

    /// <summary>
    /// Executes the register&lt;trequest, tresult&gt; operation.
    /// </summary>
    IDisposable Register<TRequest, TResult>(
        Func<InteractionContext<TRequest>, CancellationToken, ValueTask<TResult>> handler,
        InteractionHandlerRegistrationOptions options);

    /// <summary>
    /// Executes the handle async&lt;trequest, tresult&gt; operation.
    /// </summary>
    ValueTask<InteractionResult<TResult>> HandleAsync<TRequest, TResult>(
        TRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the handle async&lt;trequest, tresult&gt; operation.
    /// </summary>
    ValueTask<InteractionResult<TResult>> HandleAsync<TRequest, TResult>(
        TRequest request,
        InteractionDispatchContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the revoke plugin operation.
    /// </summary>
    int RevokePlugin(string pluginId);

    /// <summary>
    /// Executes the revoke contribution operation.
    /// </summary>
    int RevokeContribution(string contributionId);

    /// <summary>
    /// Gets get modal queue snapshot.
    /// </summary>
    PresentationQueueSnapshot GetModalQueueSnapshot(string? windowId = null) => new(
        PresentationQueueOptions.DefaultModalInteractionPendingCapacity,
        PendingCount: 0,
        InFlightCount: 0,
        PeakPendingCount: 0,
        RejectedCount: 0);
}
