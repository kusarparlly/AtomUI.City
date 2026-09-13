using System.Text.Json;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security;

public sealed class FileCredentialStore : ICredentialStore
{
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private readonly string _credentialsPath;
    private readonly IHostDiagnostics? _diagnostics;

    public FileCredentialStore(string rootPath, IHostDiagnostics? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        RootPath = Path.GetFullPath(rootPath);
        _credentialsPath = Path.Combine(RootPath, "credentials");
        _diagnostics = diagnostics;
    }

    public string RootPath { get; }

    public async ValueTask<SecurityStoreResult<AccountCredentialSnapshot>> GetAsync(
        SecurityAccountKey accountKey,
        string resourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        if (!await EnterAsync(cancellationToken).ConfigureAwait(false))
        {
            return SecurityStoreResult<AccountCredentialSnapshot>.Failed(
                SecurityStoreResultStatus.Cancelled,
                "Credential loading was cancelled.");
        }

        try
        {
            var path = GetCredentialPath(accountKey, resourceName);
            if (!File.Exists(path))
            {
                return Failure<AccountCredentialSnapshot>(
                    "get",
                    accountKey,
                    SecurityStoreResultStatus.NotFound,
                    "The credential does not exist.");
            }

            var document = await SecurityFilePersistence.ReadJsonAsync(
                    path,
                    SecurityJsonSerializerContext.Default.CredentialDocument,
                    cancellationToken)
                .ConfigureAwait(false);
            if (document is null)
            {
                return Failure<AccountCredentialSnapshot>(
                    "get",
                    accountKey,
                    SecurityStoreResultStatus.InvalidData,
                    "The credential document is empty.");
            }

            if (document.SchemaVersion > SecurityFilePersistence.SchemaVersion)
            {
                return Failure<AccountCredentialSnapshot>(
                    "get",
                    accountKey,
                    SecurityStoreResultStatus.UnsupportedSchema,
                    "The credential document uses a newer schema.");
            }

            if (document.SchemaVersion != SecurityFilePersistence.SchemaVersion)
            {
                return Failure<AccountCredentialSnapshot>(
                    "get",
                    accountKey,
                    SecurityStoreResultStatus.InvalidData,
                    "The credential document schema is invalid.");
            }

            var credential = document.ToSnapshot();
            if (!credential.AccountKey.Equals(accountKey) ||
                !string.Equals(credential.ResourceName, resourceName.Trim(), StringComparison.Ordinal))
            {
                return Failure<AccountCredentialSnapshot>(
                    "get",
                    accountKey,
                    SecurityStoreResultStatus.InvalidData,
                    "The credential identity does not match its storage location.");
            }

            WriteDiagnostic("get", SecurityStoreResultStatus.Success, accountKey);
            return SecurityStoreResult<AccountCredentialSnapshot>.Success(credential);
        }
        catch (Exception exception) when (TryMapException(exception, cancellationToken, out var status))
        {
            return Failure<AccountCredentialSnapshot>("get", accountKey, status, "The credential is invalid or unreadable.", exception);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async ValueTask<SecurityStoreResult<bool>> SaveAsync(
        AccountCredentialSnapshot credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);
        if (credential.SchemaVersion != AccountCredentialSnapshot.CurrentSchemaVersion)
        {
            return Failure<bool>(
                "save",
                credential.AccountKey,
                SecurityStoreResultStatus.UnsupportedSchema,
                "Only the current credential schema can be written.");
        }

        if (!await EnterAsync(cancellationToken).ConfigureAwait(false))
        {
            return SecurityStoreResult<bool>.Failed(SecurityStoreResultStatus.Cancelled, "Credential saving was cancelled.");
        }

        try
        {
            await SecurityFilePersistence.WriteJsonAtomicAsync(
                GetCredentialPath(credential.AccountKey, credential.ResourceName),
                CredentialDocument.FromSnapshot(credential),
                SecurityJsonSerializerContext.Default.CredentialDocument,
                cancellationToken).ConfigureAwait(false);
            WriteDiagnostic("save", SecurityStoreResultStatus.Success, credential.AccountKey);
            return SecurityStoreResult<bool>.Success(true);
        }
        catch (Exception exception) when (TryMapException(exception, cancellationToken, out var status))
        {
            return Failure<bool>("save", credential.AccountKey, status, "Credential storage could not be written.", exception);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async ValueTask<SecurityStoreResult<bool>> RemoveAsync(
        SecurityAccountKey accountKey,
        string resourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        return await RemoveCoreAsync(
            accountKey,
            GetCredentialPath(accountKey, resourceName),
            recursive: false,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<SecurityStoreResult<bool>> RemoveAllAsync(
        SecurityAccountKey accountKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountKey);
        return await RemoveCoreAsync(
            accountKey,
            GetAccountDirectory(accountKey),
            recursive: true,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SecurityStoreResult<bool>> RemoveCoreAsync(
        SecurityAccountKey accountKey,
        string path,
        bool recursive,
        CancellationToken cancellationToken)
    {
        if (!await EnterAsync(cancellationToken).ConfigureAwait(false))
        {
            return SecurityStoreResult<bool>.Failed(SecurityStoreResultStatus.Cancelled, "Credential removal was cancelled.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var removed = recursive ? Directory.Exists(path) : File.Exists(path);
            if (recursive && removed)
            {
                Directory.Delete(path, recursive: true);
            }
            else if (removed)
            {
                File.Delete(path);
            }

            WriteDiagnostic(recursive ? "remove-all" : "remove", SecurityStoreResultStatus.Success, accountKey);
            return SecurityStoreResult<bool>.Success(removed);
        }
        catch (Exception exception) when (TryMapException(exception, cancellationToken, out var status))
        {
            return Failure<bool>(
                recursive ? "remove-all" : "remove",
                accountKey,
                status,
                "Credential storage could not be removed.",
                exception);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    private string GetAccountDirectory(SecurityAccountKey accountKey) =>
        Path.Combine(_credentialsPath, accountKey.ToStorageKey());

    private string GetCredentialPath(SecurityAccountKey accountKey, string resourceName) =>
        Path.Combine(GetAccountDirectory(accountKey), $"{SecurityFilePersistence.HashResourceName(resourceName)}.json");

    private async Task<bool> EnterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private SecurityStoreResult<T> Failure<T>(
        string operation,
        SecurityAccountKey accountKey,
        SecurityStoreResultStatus status,
        string message,
        Exception? exception = null)
    {
        WriteDiagnostic(operation, status, accountKey, exception);
        return SecurityStoreResult<T>.Failed(status, message, exception);
    }

    private void WriteDiagnostic(
        string operation,
        SecurityStoreResultStatus status,
        SecurityAccountKey accountKey,
        Exception? exception = null)
    {
        SecurityDiagnostics.Write(
            _diagnostics,
            status == SecurityStoreResultStatus.Success
                ? SecurityDiagnosticIds.CredentialPersistenceCompleted
                : SecurityDiagnosticIds.CredentialPersistenceFailed,
            status == SecurityStoreResultStatus.Success
                ? "Credential persistence operation completed."
                : "Credential persistence operation failed.",
            status == SecurityStoreResultStatus.Success
                ? HostDiagnosticSeverity.Info
                : HostDiagnosticSeverity.Error,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["operation"] = operation,
                ["storeKind"] = "credential",
                ["accountIdHash"] = accountKey.ToStorageKey()[..16],
                ["schemaVersion"] = SecurityFilePersistence.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["resultStatus"] = status.ToString(),
                ["exceptionType"] = exception?.GetType().FullName,
            });
    }

    private static bool TryMapException(
        Exception exception,
        CancellationToken cancellationToken,
        out SecurityStoreResultStatus status)
    {
        status = exception switch
        {
            OperationCanceledException when cancellationToken.IsCancellationRequested => SecurityStoreResultStatus.Cancelled,
            JsonException or FormatException or ArgumentException => SecurityStoreResultStatus.InvalidData,
            UnauthorizedAccessException => SecurityStoreResultStatus.AccessDenied,
            IOException => SecurityStoreResultStatus.IoFailed,
            _ => SecurityStoreResultStatus.IoFailed,
        };
        return true;
    }

    internal sealed record CredentialDocument(
        int SchemaVersion,
        string AuthenticationScheme,
        string Authority,
        string? TenantId,
        string SubjectId,
        string ResourceName,
        string AccessToken,
        string? RefreshToken,
        string Scheme,
        DateTimeOffset? ExpiresAt)
    {
        public static CredentialDocument FromSnapshot(AccountCredentialSnapshot snapshot) => new(
            SecurityFilePersistence.SchemaVersion,
            snapshot.AccountKey.AuthenticationScheme,
            snapshot.AccountKey.Authority,
            snapshot.AccountKey.TenantId,
            snapshot.AccountKey.SubjectId,
            snapshot.ResourceName,
            snapshot.AccessToken,
            snapshot.RefreshToken,
            snapshot.Scheme,
            snapshot.ExpiresAt);

        public AccountCredentialSnapshot ToSnapshot() => new(
            new SecurityAccountKey(AuthenticationScheme, Authority, TenantId, SubjectId),
            ResourceName,
            AccessToken,
            Scheme,
            RefreshToken,
            ExpiresAt);
    }
}
