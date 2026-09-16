namespace AtomUI.City.Data;

/// <summary>
/// Defines the supported data access mode values.
/// </summary>
public enum DataAccessMode
{
    /// <summary>
    /// Represents the query value.
    /// </summary>
    Query,
    /// <summary>
    /// Represents the mutation value.
    /// </summary>
    Mutation,
    /// <summary>
    /// Represents the subscription value.
    /// </summary>
    Subscription,
    /// <summary>
    /// Represents the upload value.
    /// </summary>
    Upload,
    /// <summary>
    /// Represents the download value.
    /// </summary>
    Download,
}
