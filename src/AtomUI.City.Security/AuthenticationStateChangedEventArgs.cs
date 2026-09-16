namespace AtomUI.City.Security;

/// <summary>
/// Represents authentication state changed event args.
/// </summary>
public sealed class AuthenticationStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <c>AuthenticationStateChangedEventArgs</c> type.
    /// </summary>
    public AuthenticationStateChangedEventArgs(
        AuthenticationStateSnapshot previous,
        AuthenticationStateSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        if (current.Revision <= previous.Revision)
        {
            throw new ArgumentException(
                "The current authentication snapshot revision must follow the previous revision.",
                nameof(current));
        }

        Previous = previous;
        Current = current;
    }

    /// <summary>
    /// Gets previous.
    /// </summary>
    public AuthenticationStateSnapshot Previous { get; }

    /// <summary>
    /// Gets current.
    /// </summary>
    public AuthenticationStateSnapshot Current { get; }
}
