namespace AtomUI.City.Data;

/// <summary>
/// Represents data connection owner.
/// </summary>
public readonly record struct DataConnectionOwner
{
    /// <summary>
    /// Initializes a new instance of the <c>DataConnectionOwner</c> type.
    /// </summary>
    public DataConnectionOwner(DataConnectionOwnerKind kind, string id)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Data connection owner kind is not supported.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Kind = kind;
        Id = id;
    }

    /// <summary>
    /// Gets kind.
    /// </summary>
    public DataConnectionOwnerKind Kind { get; }

    /// <summary>
    /// Gets id.
    /// </summary>
    public string? Id { get; }

    /// <summary>
    /// Gets none.
    /// </summary>
    public static DataConnectionOwner None { get; } = new();
}
