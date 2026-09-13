using System.Text.Json;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security;

public sealed class FileAccountSessionStore : IAccountSessionStore
{
    private const string AccountFileName = "account.json";
    private const string ActiveAccountFileName = "active-account.json";

    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private readonly string _rootPath;
    private readonly string _accountsPath;
    private readonly IHostDiagnostics? _diagnostics;

    public FileAccountSessionStore(string rootPath, IHostDiagnostics? diagnostics = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = Path.GetFullPath(rootPath);
        _accountsPath = Path.Combine(_rootPath, "accounts");
        _diagnostics = diagnostics;
    }

    public string RootPath => _rootPath;

    public async ValueTask<SecurityStoreResult<IReadOnlyList<AccountRecordSnapshot>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        if (!await EnterAsync(cancellationToken).ConfigureAwait(false))
        {
            return SecurityStoreResult<IReadOnlyList<AccountRecordSnapshot>>.Failed(
                SecurityStoreResultStatus.Cancelled,
                "Account listing was cancelled.");
        }

        try
        {
            if (!Directory.Exists(_accountsPath))
            {
                return SecurityStoreResult<IReadOnlyList<AccountRecordSnapshot>>.Success(
                    Array.Empty<AccountRecordSnapshot>());
            }

            var accounts = new List<AccountRecordSnapshot>();
            foreach (var directory in Directory.EnumerateDirectories(_accountsPath).Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await ReadAccountCoreAsync(
                    Path.Combine(directory, AccountFileName),
                    expectedAccountKey: null,
                    cancellationToken).ConfigureAwait(false);
                if (!result.Succeeded)
                {
                    return SecurityStoreResult<IReadOnlyList<AccountRecordSnapshot>>.Failed(
                        result.Status,
                        result.Message,
                        result.Exception);
                }

                accounts.Add(result.Value!);
            }

            WriteDiagnostic("list", SecurityStoreResultStatus.Success, accountKey: null);
            return SecurityStoreResult<IReadOnlyList<AccountRecordSnapshot>>.Success(
                Array.AsReadOnly(accounts.ToArray()));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure<IReadOnlyList<AccountRecordSnapshot>>(
                "list",
                accountKey: null,
                SecurityStoreResultStatus.Cancelled,
                "Account listing was cancelled.");
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure<IReadOnlyList<AccountRecordSnapshot>>(
                "list",
                accountKey: null,
                SecurityStoreResultStatus.AccessDenied,
                "Account storage could not be read.",
                exception);
        }
        catch (IOException exception)
        {
            return Failure<IReadOnlyList<AccountRecordSnapshot>>(
                "list",
                accountKey: null,
                SecurityStoreResultStatus.IoFailed,
                "Account storage could not be read.",
                exception);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async ValueTask<SecurityStoreResult<AccountRecordSnapshot>> GetAsync(
        SecurityAccountKey accountKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountKey);
        if (!await EnterAsync(cancellationToken).ConfigureAwait(false))
        {
            return SecurityStoreResult<AccountRecordSnapshot>.Failed(
                SecurityStoreResultStatus.Cancelled,
                "Account loading was cancelled.");
        }

        try
        {
            return await ReadAccountCoreAsync(GetAccountFilePath(accountKey), accountKey, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async ValueTask<SecurityStoreResult<bool>> SaveAsync(
        AccountRecordSnapshot account,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.Profile.SchemaVersion != AccountProfileSnapshot.CurrentSchemaVersion ||
            account.Permissions.SchemaVersion != PersistedPermissionSnapshot.CurrentSchemaVersion)
        {
            return Failure<bool>(
                "save",
                account.AccountKey,
                SecurityStoreResultStatus.UnsupportedSchema,
                "Only the current account and permission schema can be written.");
        }

        if (!await EnterAsync(cancellationToken).ConfigureAwait(false))
        {
            return SecurityStoreResult<bool>.Failed(SecurityStoreResultStatus.Cancelled, "Account saving was cancelled.");
        }

        try
        {
            var document = AccountDocument.FromSnapshot(account);
            await SecurityFilePersistence.WriteJsonAtomicAsync(
                GetAccountFilePath(account.AccountKey),
                document,
                SecurityJsonSerializerContext.Default.AccountDocument,
                cancellationToken).ConfigureAwait(false);
            WriteDiagnostic("save", SecurityStoreResultStatus.Success, account.AccountKey);
            return SecurityStoreResult<bool>.Success(true);
        }
        catch (Exception exception) when (TryMapException(exception, cancellationToken, out var status))
        {
            return Failure<bool>("save", account.AccountKey, status, "Account storage could not be written.", exception);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async ValueTask<SecurityStoreResult<bool>> RemoveAsync(
        SecurityAccountKey accountKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountKey);
        if (!await EnterAsync(cancellationToken).ConfigureAwait(false))
        {
            return SecurityStoreResult<bool>.Failed(SecurityStoreResultStatus.Cancelled, "Account removal was cancelled.");
        }

        var directory = GetAccountDirectory(accountKey);
        var activePointerCleared = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var active = await ReadActiveAccountCoreAsync(cancellationToken).ConfigureAwait(false);
            if (!active.Succeeded)
            {
                return SecurityStoreResult<bool>.Failed(active.Status, active.Message, active.Exception);
            }

            if (active.Value?.Equals(accountKey) == true)
            {
                await WriteActiveAccountCoreAsync(accountKey: null, cancellationToken).ConfigureAwait(false);
                activePointerCleared = true;
            }

            var removed = Directory.Exists(directory);
            if (removed)
            {
                Directory.Delete(directory, recursive: true);
            }

            WriteDiagnostic("remove", SecurityStoreResultStatus.Success, accountKey);
            return SecurityStoreResult<bool>.Success(removed);
        }
        catch (Exception exception) when (TryMapException(exception, cancellationToken, out var status))
        {
            if (activePointerCleared && Directory.Exists(directory))
            {
                try
                {
                    await WriteActiveAccountCoreAsync(accountKey, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // Preserve the original removal failure; diagnostics report the authoritative result.
                }
            }

            return Failure<bool>("remove", accountKey, status, "Account storage could not be removed.", exception);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async ValueTask<SecurityStoreResult<SecurityAccountKey?>> GetLastActiveAccountAsync(
        CancellationToken cancellationToken = default)
    {
        if (!await EnterAsync(cancellationToken).ConfigureAwait(false))
        {
            return SecurityStoreResult<SecurityAccountKey?>.Failed(
                SecurityStoreResultStatus.Cancelled,
                "Active account loading was cancelled.");
        }

        try
        {
            var result = await ReadActiveAccountCoreAsync(cancellationToken).ConfigureAwait(false);
            if (result.Succeeded)
            {
                WriteDiagnostic("get-active", SecurityStoreResultStatus.Success, result.Value);
            }

            return result;
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async ValueTask<SecurityStoreResult<bool>> SetLastActiveAccountAsync(
        SecurityAccountKey? accountKey,
        CancellationToken cancellationToken = default)
    {
        if (!await EnterAsync(cancellationToken).ConfigureAwait(false))
        {
            return SecurityStoreResult<bool>.Failed(
                SecurityStoreResultStatus.Cancelled,
                "Active account update was cancelled.");
        }

        try
        {
            if (accountKey is not null && !File.Exists(GetAccountFilePath(accountKey)))
            {
                return Failure<bool>(
                    "set-active",
                    accountKey,
                    SecurityStoreResultStatus.NotFound,
                    "The active account must reference a saved account.");
            }

            await WriteActiveAccountCoreAsync(accountKey, cancellationToken).ConfigureAwait(false);
            WriteDiagnostic("set-active", SecurityStoreResultStatus.Success, accountKey);
            return SecurityStoreResult<bool>.Success(true);
        }
        catch (Exception exception) when (TryMapException(exception, cancellationToken, out var status))
        {
            return Failure<bool>("set-active", accountKey, status, "The active account pointer could not be written.", exception);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    private async Task<SecurityStoreResult<AccountRecordSnapshot>> ReadAccountCoreAsync(
        string path,
        SecurityAccountKey? expectedAccountKey,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Failure<AccountRecordSnapshot>(
                "get",
                expectedAccountKey,
                SecurityStoreResultStatus.NotFound,
                "The account does not exist.");
        }

        try
        {
            var document = await SecurityFilePersistence.ReadJsonAsync(
                    path,
                    SecurityJsonSerializerContext.Default.AccountDocument,
                    cancellationToken)
                .ConfigureAwait(false);
            if (document is null)
            {
                return Failure<AccountRecordSnapshot>(
                    "get",
                    expectedAccountKey,
                    SecurityStoreResultStatus.InvalidData,
                    "The account document is empty.");
            }

            if (document.SchemaVersion > SecurityFilePersistence.SchemaVersion)
            {
                return Failure<AccountRecordSnapshot>(
                    "get",
                    expectedAccountKey,
                    SecurityStoreResultStatus.UnsupportedSchema,
                    "The account document uses a newer schema.");
            }

            if (document.SchemaVersion != SecurityFilePersistence.SchemaVersion)
            {
                return Failure<AccountRecordSnapshot>(
                    "get",
                    expectedAccountKey,
                    SecurityStoreResultStatus.InvalidData,
                    "The account document schema is invalid.");
            }

            var snapshot = document.ToSnapshot();
            if (expectedAccountKey is not null && !snapshot.AccountKey.Equals(expectedAccountKey))
            {
                return Failure<AccountRecordSnapshot>(
                    "get",
                    expectedAccountKey,
                    SecurityStoreResultStatus.InvalidData,
                    "The account document identity does not match its storage key.");
            }

            var directoryName = Path.GetFileName(Path.GetDirectoryName(path));
            if (!string.Equals(directoryName, snapshot.AccountKey.ToStorageKey(), StringComparison.Ordinal))
            {
                return Failure<AccountRecordSnapshot>(
                    "get",
                    snapshot.AccountKey,
                    SecurityStoreResultStatus.InvalidData,
                    "The account document is stored under the wrong account identity.");
            }

            WriteDiagnostic("get", SecurityStoreResultStatus.Success, snapshot.AccountKey);
            return SecurityStoreResult<AccountRecordSnapshot>.Success(snapshot);
        }
        catch (Exception exception) when (TryMapException(exception, cancellationToken, out var status))
        {
            return Failure<AccountRecordSnapshot>("get", expectedAccountKey, status, "The account document is invalid or unreadable.", exception);
        }
    }

    private async Task<SecurityStoreResult<SecurityAccountKey?>> ReadActiveAccountCoreAsync(
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(_rootPath, ActiveAccountFileName);
        if (!File.Exists(path))
        {
            return SecurityStoreResult<SecurityAccountKey?>.Success(null);
        }

        try
        {
            var document = await SecurityFilePersistence.ReadJsonAsync(
                    path,
                    SecurityJsonSerializerContext.Default.ActiveAccountDocument,
                    cancellationToken)
                .ConfigureAwait(false);
            if (document is null || document.SchemaVersion != SecurityFilePersistence.SchemaVersion)
            {
                var status = document?.SchemaVersion > SecurityFilePersistence.SchemaVersion
                    ? SecurityStoreResultStatus.UnsupportedSchema
                    : SecurityStoreResultStatus.InvalidData;
                return Failure<SecurityAccountKey?>(
                    "get-active",
                    accountKey: null,
                    status,
                    "The active account document has an invalid schema.");
            }

            if (document.Account is null)
            {
                return SecurityStoreResult<SecurityAccountKey?>.Success(null);
            }

            var key = document.Account.ToAccountKey();
            if (!File.Exists(GetAccountFilePath(key)))
            {
                return Failure<SecurityAccountKey?>(
                    "get-active",
                    key,
                    SecurityStoreResultStatus.InvalidData,
                    "The active account pointer references a missing account.");
            }

            return SecurityStoreResult<SecurityAccountKey?>.Success(key);
        }
        catch (Exception exception) when (TryMapException(exception, cancellationToken, out var status))
        {
            return Failure<SecurityAccountKey?>("get-active", accountKey: null, status, "The active account document is invalid or unreadable.", exception);
        }
    }

    private Task WriteActiveAccountCoreAsync(SecurityAccountKey? accountKey, CancellationToken cancellationToken)
    {
        return SecurityFilePersistence.WriteJsonAtomicAsync(
            Path.Combine(_rootPath, ActiveAccountFileName),
            new ActiveAccountDocument(
                SecurityFilePersistence.SchemaVersion,
                accountKey is null ? null : AccountKeyDocument.FromKey(accountKey)),
            SecurityJsonSerializerContext.Default.ActiveAccountDocument,
            cancellationToken);
    }

    private string GetAccountDirectory(SecurityAccountKey accountKey) =>
        Path.Combine(_accountsPath, accountKey.ToStorageKey());

    private string GetAccountFilePath(SecurityAccountKey accountKey) =>
        Path.Combine(GetAccountDirectory(accountKey), AccountFileName);

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
        SecurityAccountKey? accountKey,
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
        SecurityAccountKey? accountKey,
        Exception? exception = null)
    {
        SecurityDiagnostics.Write(
            _diagnostics,
            status == SecurityStoreResultStatus.Success
                ? SecurityDiagnosticIds.AccountPersistenceCompleted
                : SecurityDiagnosticIds.AccountPersistenceFailed,
            status == SecurityStoreResultStatus.Success
                ? "Account persistence operation completed."
                : "Account persistence operation failed.",
            status == SecurityStoreResultStatus.Success
                ? HostDiagnosticSeverity.Info
                : HostDiagnosticSeverity.Error,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["operation"] = operation,
                ["storeKind"] = "account",
                ["accountIdHash"] = accountKey?.ToStorageKey()[..16],
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

    internal sealed record AccountDocument(
        int SchemaVersion,
        AccountKeyDocument Account,
        string DisplayName,
        string? AvatarUri,
        DateTimeOffset LastUsedAt,
        long PermissionRevision,
        DateTimeOffset PermissionIssuedAt,
        DateTimeOffset PermissionExpiresAt,
        string[] Permissions)
    {
        public static AccountDocument FromSnapshot(AccountRecordSnapshot snapshot) => new(
            SecurityFilePersistence.SchemaVersion,
            AccountKeyDocument.FromKey(snapshot.AccountKey),
            snapshot.Profile.DisplayName,
            snapshot.Profile.AvatarUri?.AbsoluteUri,
            snapshot.Profile.LastUsedAt,
            snapshot.Permissions.Revision,
            snapshot.Permissions.IssuedAt,
            snapshot.Permissions.ExpiresAt,
            snapshot.Permissions.Permissions.ToArray());

        public AccountRecordSnapshot ToSnapshot()
        {
            var key = Account.ToAccountKey();
            Uri? avatarUri = null;
            if (AvatarUri is not null && !Uri.TryCreate(AvatarUri, UriKind.Absolute, out avatarUri))
            {
                throw new FormatException("The account avatar URI is invalid.");
            }

            return new AccountRecordSnapshot(
                new AccountProfileSnapshot(key, DisplayName, avatarUri, LastUsedAt),
                new PersistedPermissionSnapshot(
                    key,
                    Permissions ?? throw new FormatException("The permission list is missing."),
                    PermissionRevision,
                    PermissionIssuedAt,
                    PermissionExpiresAt));
        }
    }

    internal sealed record ActiveAccountDocument(int SchemaVersion, AccountKeyDocument? Account);

    internal sealed record AccountKeyDocument(
        string AuthenticationScheme,
        string Authority,
        string? TenantId,
        string SubjectId)
    {
        public static AccountKeyDocument FromKey(SecurityAccountKey key) =>
            new(key.AuthenticationScheme, key.Authority, key.TenantId, key.SubjectId);

        public SecurityAccountKey ToAccountKey() =>
            new(AuthenticationScheme, Authority, TenantId, SubjectId);
    }
}
