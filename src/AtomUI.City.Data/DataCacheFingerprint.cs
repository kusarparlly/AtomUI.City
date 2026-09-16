using System.Security.Cryptography;
using System.Text;

namespace AtomUI.City.Data;

/// <summary>
/// Represents data cache fingerprint.
/// </summary>
public static class DataCacheFingerprint
{
    /// <summary>
    /// Executes the create operation.
    /// </summary>
    public static string Create(string endpoint, string method, ReadOnlySpan<byte> canonicalPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, endpoint.Trim());
        Append(hash, method.Trim().ToUpperInvariant());
        hash.AppendData(canonicalPayload);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>
    /// Executes the create operation.
    /// </summary>
    public static string Create(string endpoint, string method, string canonicalPayload)
    {
        ArgumentNullException.ThrowIfNull(canonicalPayload);
        return Create(endpoint, method, Encoding.UTF8.GetBytes(canonicalPayload));
    }

    private static void Append(IncrementalHash hash, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
