namespace AtomUI.City.EventBus;

/// <summary>
/// Defines the contract for ievent handler&lt;tevent&gt;.
/// </summary>
public interface IEventHandler<TEvent>
{
    /// <summary>
    /// Executes the handle async operation.
    /// </summary>
    ValueTask HandleAsync(EventContext<TEvent> context);
}
