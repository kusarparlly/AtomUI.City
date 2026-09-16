namespace AtomUI.City.Security;

/// <summary>
/// Defines the contract for icommand authorization descriptor provider.
/// </summary>
public interface ICommandAuthorizationDescriptorProvider
{
    /// <summary>
    /// Occurs when descriptor changed.
    /// </summary>
    event EventHandler<CommandAuthorizationChangedEventArgs>? DescriptorChanged;

    /// <summary>
    /// Executes the get descriptor async operation.
    /// </summary>
    ValueTask<CommandAuthorizationDescriptor?> GetDescriptorAsync(
        CommandAuthorizationContext context,
        CancellationToken cancellationToken = default);
}
