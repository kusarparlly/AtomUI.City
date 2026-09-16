namespace AtomUI.City.Data;

/// <summary>
/// Represents data credential.
/// </summary>
public sealed record DataCredential
{
    /// <summary>
    /// Initializes a new instance of the <c>DataCredential</c> type.
    /// </summary>
    public DataCredential(string scheme, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);

        Scheme = scheme;
        Parameter = parameter;
    }

    /// <summary>
    /// Gets scheme.
    /// </summary>
    public string Scheme { get; }

    /// <summary>
    /// Gets parameter.
    /// </summary>
    public string Parameter { get; }

    /// <summary>
    /// Executes the bearer operation.
    /// </summary>
    public static DataCredential Bearer(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return new DataCredential("Bearer", token);
    }

    /// <summary>
    /// Gets to string.
    /// </summary>
    public override string ToString() =>
        $"{nameof(DataCredential)} {{ Scheme = {Scheme}, Parameter = [REDACTED] }}";
}
