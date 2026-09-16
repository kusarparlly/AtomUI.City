using System.Security.Cryptography;
using System.Text;

namespace AtomUI.City.Security;

/// <summary>
/// Represents security account key.
/// </summary>
public sealed class SecurityAccountKey : IEquatable<SecurityAccountKey>
{
    /// <summary>
    /// Initializes a new instance of the <c>SecurityAccountKey</c> type.
    /// </summary>
    public SecurityAccountKey(
        string authenticationScheme,
        string authority,
        string? tenantId,
        string subjectId)
    {
        AuthenticationScheme = Normalize(authenticationScheme, nameof(authenticationScheme), lowerInvariant: true);
        Authority = NormalizeAuthority(authority);
        TenantId = tenantId is null ? null : Normalize(tenantId, nameof(tenantId), lowerInvariant: false);
        SubjectId = Normalize(subjectId, nameof(subjectId), lowerInvariant: false);
    }

    /// <summary>
    /// Gets authentication scheme.
    /// </summary>
    public string AuthenticationScheme { get; }

    /// <summary>
    /// Gets authority.
    /// </summary>
    public string Authority { get; }

    /// <summary>
    /// Gets tenant id.
    /// </summary>
    public string? TenantId { get; }

    /// <summary>
    /// Gets subject id.
    /// </summary>
    public string SubjectId { get; }

    /// <summary>
    /// Executes the to storage key operation.
    /// </summary>
    public string ToStorageKey()
    {
        var canonical = string.Join('\n', AuthenticationScheme, Authority, TenantId ?? string.Empty, SubjectId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Executes the equals operation.
    /// </summary>
    public bool Equals(SecurityAccountKey? other)
    {
        return other is not null
            && string.Equals(AuthenticationScheme, other.AuthenticationScheme, StringComparison.Ordinal)
            && string.Equals(Authority, other.Authority, StringComparison.Ordinal)
            && string.Equals(TenantId, other.TenantId, StringComparison.Ordinal)
            && string.Equals(SubjectId, other.SubjectId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets equals.
    /// </summary>
    public override bool Equals(object? obj) => Equals(obj as SecurityAccountKey);

    /// <summary>
    /// Gets get hash code.
    /// </summary>
    public override int GetHashCode() => HashCode.Combine(AuthenticationScheme, Authority, TenantId, SubjectId);

    /// <summary>
    /// Gets to string.
    /// </summary>
    public override string ToString() => $"account:{ToStorageKey()[..16]}";

    private static string NormalizeAuthority(string authority)
    {
        var value = Normalize(authority, nameof(authority), lowerInvariant: false).TrimEnd('/');
        if (value.Length == 0)
        {
            throw new ArgumentException("Authority cannot contain only separators.", nameof(authority));
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return value;
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
        };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private static string Normalize(string value, string parameterName, bool lowerInvariant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException("Account identity fields cannot contain control characters.", parameterName);
        }

        return lowerInvariant ? normalized.ToLowerInvariant() : normalized;
    }
}
