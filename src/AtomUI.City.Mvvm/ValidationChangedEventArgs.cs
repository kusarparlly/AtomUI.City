namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents validation changed event args.
/// </summary>
public sealed class ValidationChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationChangedEventArgs"/> type.
    /// </summary>
    internal ValidationChangedEventArgs(
        string key,
        ValidationStatus status,
        IReadOnlyList<string> errors,
        IReadOnlyList<ValidationMessage> messages,
        Guid? ownerScopeId)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(messages);

        Key = key;
        Status = status;
        Errors = Array.AsReadOnly(errors.ToArray());
        Messages = Array.AsReadOnly(messages.ToArray());
        OwnerScopeId = ownerScopeId;
    }

    /// <summary>
    /// Gets key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets status.
    /// </summary>
    public ValidationStatus Status { get; }

    /// <summary>
    /// Gets errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Gets messages.
    /// </summary>
    public IReadOnlyList<ValidationMessage> Messages { get; }

    /// <summary>
    /// Gets owner scope id.
    /// </summary>
    public Guid? OwnerScopeId { get; }
}
