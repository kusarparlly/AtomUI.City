namespace AtomUI.City.Data;

/// <summary>
/// Represents data cache lookup&lt;tresponse&gt;.
/// </summary>
public sealed class DataCacheLookup<TResponse>
{
    private DataCacheLookup(bool hit, TResponse? value)
    {
        IsHit = hit;
        Value = value;
    }

    /// <summary>
    /// Gets a value indicating whether is hit.
    /// </summary>
    public bool IsHit { get; }

    /// <summary>
    /// Gets value.
    /// </summary>
    public TResponse? Value { get; }

    /// <summary>
    /// Executes the miss operation.
    /// </summary>
    public static DataCacheLookup<TResponse> Miss()
    {
        return new DataCacheLookup<TResponse>(hit: false, value: default);
    }

    /// <summary>
    /// Executes the hit operation.
    /// </summary>
    public static DataCacheLookup<TResponse> Hit(TResponse? value)
    {
        return new DataCacheLookup<TResponse>(hit: true, value);
    }
}
