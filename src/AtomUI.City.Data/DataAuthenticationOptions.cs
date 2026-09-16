namespace AtomUI.City.Data;

/// <summary>
/// Represents data authentication options.
/// </summary>
public sealed class DataAuthenticationOptions
{
    private DataAuthenticationOptions(
        DataAuthenticationMode mode,
        string? scheme)
    {
        Mode = mode;
        Scheme = scheme;
    }

    /// <summary>
    /// Gets mode.
    /// </summary>
    public DataAuthenticationMode Mode { get; }

    /// <summary>
    /// Gets scheme.
    /// </summary>
    public string? Scheme { get; }

    /// <summary>
    /// Gets anonymous.
    /// </summary>
    public static DataAuthenticationOptions Anonymous { get; } =
        new(DataAuthenticationMode.Anonymous, scheme: null);

    /// <summary>
    /// Executes the bearer operation.
    /// </summary>
    public static DataAuthenticationOptions Bearer(string scheme = "Bearer")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);

        return new DataAuthenticationOptions(DataAuthenticationMode.Bearer, scheme);
    }
}
