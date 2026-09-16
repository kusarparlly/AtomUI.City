namespace AtomUI.City.Localization;

/// <summary>
/// Represents language package.
/// </summary>
public sealed class LanguagePackage : IDisposable
{
    private readonly IReadOnlyDictionary<string, string> _strings;
    private int _disposed;

    private LanguagePackage(
        LanguagePackageDescriptor descriptor,
        IReadOnlyDictionary<string, string> strings)
    {
        Descriptor = descriptor;
        _strings = new Dictionary<string, string>(strings, StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets descriptor.
    /// </summary>
    public LanguagePackageDescriptor Descriptor { get; }

    /// <summary>
    /// Gets a value indicating whether is disposed.
    /// </summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Executes the create operation.
    /// </summary>
    public static LanguagePackage Create(
        LanguagePackageDescriptor descriptor,
        IReadOnlyDictionary<string, string> strings)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(strings);
        if (strings.Any(resource => string.IsNullOrWhiteSpace(resource.Key) || resource.Value is null))
        {
            throw new ArgumentException(
                "Language package resources require non-empty keys and non-null values.",
                nameof(strings));
        }

        return new LanguagePackage(descriptor, strings);
    }

    /// <summary>
    /// Executes the try get string operation.
    /// </summary>
    public bool TryGetString(string key, out string value)
    {
        if (IsDisposed)
        {
            value = string.Empty;

            return false;
        }

        return _strings.TryGetValue(key, out value!);
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        Interlocked.Exchange(ref _disposed, 1);
    }
}
