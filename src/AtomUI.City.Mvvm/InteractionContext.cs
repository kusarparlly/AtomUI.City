namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents interaction context&lt;trequest&gt;.
/// </summary>
public sealed class InteractionContext<TRequest>
{
    /// <summary>
    /// Executes the interaction context operation.
    /// </summary>
    public InteractionContext(TRequest request)
        : this(
            request,
            Guid.NewGuid(),
            activationScopeId: null,
            handlerType: null)
    {
    }

    internal InteractionContext(
        TRequest request,
        Guid requestId,
        Guid? activationScopeId,
        Type? handlerType)
    {
        Request = request;
        RequestId = requestId;
        ActivationScopeId = activationScopeId;
        HandlerType = handlerType;
    }

    /// <summary>
    /// Gets request.
    /// </summary>
    public TRequest Request { get; }

    /// <summary>
    /// Gets request id.
    /// </summary>
    public Guid RequestId { get; }

    /// <summary>
    /// Gets request type.
    /// </summary>
    public Type RequestType => typeof(TRequest);

    /// <summary>
    /// Gets activation scope id.
    /// </summary>
    public Guid? ActivationScopeId { get; }

    /// <summary>
    /// Gets handler type.
    /// </summary>
    public Type? HandlerType { get; }
}
