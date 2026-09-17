using System.Diagnostics;
using System.Globalization;
using AtomUI.City.State;

namespace AtomUI.City.Localization;

/// <summary>
/// Represents localization service.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private static readonly AsyncLocal<MutationExecution?> CurrentMutation = new();
    private readonly LanguagePackageRegistry _registry;
    private readonly IReadOnlyDictionary<LanguagePackageProviderKind, ILanguagePackageProvider> _providers;
    private readonly IReadOnlyList<CultureInfo> _configuredFallbackCultures;
    private readonly IPresentationLocalizationBridge _bridge;
    private readonly ILocalizationDiagnostics? _diagnostics;
    private readonly Dictionary<(string CultureName, string PackageId), LanguagePackage> _loadedPackages = [];
    private readonly Dictionary<(string CultureName, string PackageId), Task<LanguagePackageLoadResult>> _packageLoadTasks = [];
    private readonly List<ILocalizedText> _localizedTexts = [];
    private readonly Dictionary<LocalizationScopeKey, int> _activeScopes = [];
    private readonly object _packageLoadGate = new();
    private readonly object _localizedTextGate = new();
    private readonly object _scopeGate = new();
    private readonly object _mutationQueueGate = new();
    private readonly object _disposeGate = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly CancellationToken _lifetimeToken;
    private readonly WritableState<CultureState> _cultureState;
    private Task _mutationTail = Task.CompletedTask;
    private Task? _disposeTask;
    private long _packageCacheVersion;
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <c>LocalizationService</c> type.
    /// </summary>
    public LocalizationService(
        IReadOnlyList<LanguagePackageDescriptor> descriptors,
        IEnumerable<ILanguagePackageProvider> providers,
        IPresentationLocalizationBridge? bridge = null,
        ILocalizationDiagnostics? diagnostics = null)
        : this(
            LanguagePackageRegistry.CreateWithHostDescriptors(descriptors),
            providers,
            bridge,
            diagnostics,
            CultureInfo.InvariantCulture,
            CultureInfo.InvariantCulture,
            [],
            stateFactory: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>LocalizationService</c> type.
    /// </summary>
    public LocalizationService(
        LocalizationOptions options,
        IEnumerable<ILanguagePackageProvider> providers,
        IPresentationLocalizationBridge? bridge = null,
        ILocalizationDiagnostics? diagnostics = null)
        : this(
            LanguagePackageRegistry.CreateWithHostDescriptors(GetLanguagePackageDescriptors(options)),
            providers,
            bridge,
            diagnostics,
            GetDefaultCulture(options),
            GetDefaultUICulture(options),
            GetConfiguredFallbackCultures(options),
            stateFactory: null)
    {
    }

    internal LocalizationService(
        LocalizationOptions options,
        LanguagePackageRegistry registry,
        IEnumerable<ILanguagePackageProvider> providers,
        IPresentationLocalizationBridge? bridge,
        ILocalizationDiagnostics? diagnostics,
        IStateFactory? stateFactory)
        : this(
            registry,
            providers,
            bridge,
            diagnostics,
            GetDefaultCulture(options),
            GetDefaultUICulture(options),
            GetConfiguredFallbackCultures(options),
            stateFactory)
    {
    }

    private LocalizationService(
        LanguagePackageRegistry registry,
        IEnumerable<ILanguagePackageProvider> providers,
        IPresentationLocalizationBridge? bridge,
        ILocalizationDiagnostics? diagnostics,
        CultureInfo defaultCulture,
        CultureInfo defaultUICulture,
        IReadOnlyList<CultureInfo> configuredFallbackCultures,
        IStateFactory? stateFactory)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(defaultCulture);
        ArgumentNullException.ThrowIfNull(defaultUICulture);
        ArgumentNullException.ThrowIfNull(configuredFallbackCultures);

        _registry = registry;
        _lifetimeToken = _lifetimeCancellation.Token;
        _providers = BuildProviderMap(providers);
        if (configuredFallbackCultures.Any(culture => culture is null))
        {
            throw new ArgumentException(
                "Configured fallback cultures cannot contain null values.",
                nameof(configuredFallbackCultures));
        }

        _configuredFallbackCultures = configuredFallbackCultures
            .Select(CultureInfoSnapshot.Create)
            .ToArray();
        _bridge = bridge ?? NoopPresentationLocalizationBridge.Instance;
        _diagnostics = diagnostics;
        var fallbackCultures = CreateFallbackCultures(
            GetAllActiveDescriptors(_registry.Descriptors),
            defaultCulture,
            out var rejectedFallbackCulture);
        if (rejectedFallbackCulture is not null)
        {
            throw new ArgumentException(
                $"Default culture '{defaultCulture.Name}' cannot use itself as a fallback culture.",
                nameof(configuredFallbackCultures));
        }

        var initialState = new CultureState(
            defaultCulture,
            defaultUICulture,
            fallbackCultures,
            revision: 0,
            loadedPackageIds: []);
        _cultureState = stateFactory?.CreateWritable(
                initialState,
                stateName: "localization.culture",
                access: StateAccessPolicy.HostWrite)
            ?? new WritableState<CultureState>(
                initialState,
                stateName: "localization.culture",
                access: StateAccessPolicy.HostWrite);
        _registry.DescriptorsRevoked += OnDescriptorsRevoked;
    }

    /// <summary>
    /// Gets state.
    /// </summary>
    private CultureState State => _cultureState.Value;

    /// <summary>
    /// Gets culture state.
    /// </summary>
    public IReadOnlyState<CultureState> CultureState => _cultureState;

    /// <summary>
    /// Gets current culture.
    /// </summary>
    public CultureInfo CurrentCulture => State.CurrentCulture;

    /// <summary>
    /// Gets culture revision.
    /// </summary>
    public long CultureRevision => State.Revision;

    /// <summary>
    /// Executes the activate scope operation.
    /// </summary>
    public ILocalizationScopeLease ActivateScope(LocalizationLookupContext context)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);

        var keys = context.GetScopeKeys();
        if (keys.Count == 0)
        {
            throw new ArgumentException(
                "At least one module, plugin, route, or window scope id is required.",
                nameof(context));
        }

        lock (_scopeGate)
        {
            foreach (var key in keys)
            {
                _activeScopes.TryGetValue(key, out var referenceCount);
                _activeScopes[key] = checked(referenceCount + 1);
            }
        }

        return new LocalizationScopeLease(this, context);
    }

    /// <summary>
    /// Executes the set culture async operation.
    /// </summary>
    public ValueTask<LocalizationResult> SetCultureAsync(
        string cultureName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        ThrowIfDisposed();

        if (CurrentMutation.Value is { IsActive: true } currentMutation
            && ReferenceEquals(currentMutation.Owner, this))
        {
            return ValueTask.FromResult(
                LocalizationResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.ReentrantOperation,
                        "A localization mutation cannot be started from the active localization mutation callback.")));
        }

        return new ValueTask<LocalizationResult>(
            EnqueueMutationAsync(() => SetCultureCoreAsync(cultureName, cancellationToken)));
    }

    private async Task<LocalizationResult> SetCultureCoreAsync(
        string cultureName,
        CancellationToken cancellationToken)
    {
        if (IsDisposed)
        {
            return ServiceDisposedResult();
        }

        try
        {
            using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeToken);
            var operationToken = operationCancellation.Token;
            operationToken.ThrowIfCancellationRequested();
            var descriptorSnapshot = GetDescriptorSnapshot();
            if (!TryGetCulture(cultureName, out var culture, out var invalidCultureError))
            {
                WriteCultureSwitchRejected(cultureName, fallbackCultureName: null, invalidCultureError!);

                return LocalizationResult.Failed(invalidCultureError!);
            }

            var allActiveDescriptors = GetAllActiveDescriptors(descriptorSnapshot);
            var activeDescriptors = GetDescriptors(allActiveDescriptors, culture).ToArray();
            var fallbackCultures = CreateFallbackCultures(allActiveDescriptors, culture, out var rejectedFallbackCulture);
            if (rejectedFallbackCulture is not null)
            {
                var fallbackCycleError = new LocalizationError(
                    LocalizationErrorKind.InvalidCulture,
                    $"Culture '{culture.Name}' cannot use itself as a fallback culture.");
                WriteCultureSwitchRejected(culture.Name, rejectedFallbackCulture.Name, fallbackCycleError);

                return LocalizationResult.Failed(fallbackCycleError);
            }

            if (string.Equals(culture.Name, CurrentCulture.Name, StringComparison.OrdinalIgnoreCase))
            {
                WriteDiagnostic(
                    LocalizationDiagnosticIds.CultureSwitchSkipped,
                    $"Culture '{culture.Name}' is already active.",
                    LocalizationDiagnosticSeverity.Trace,
                    cultureName: culture.Name,
                    fallbackCultureName: FormatFallbackCultures(State.FallbackCultures));

                return LocalizationResult.Success();
            }

            var targetDescriptors = activeDescriptors;
            var pendingPackages = new List<LanguagePackage>();

            foreach (var descriptor in targetDescriptors)
            {
                var loadStartedAt = Stopwatch.GetTimestamp();
                var loadResult = await LoadPackageAsync(
                        descriptor,
                        cache: false,
                        operationToken)
                    .ConfigureAwait(false);

                if (!loadResult.Succeeded)
                {
                    DisposeUncachedPackages(pendingPackages);
                    if (loadResult.Error?.Kind != LocalizationErrorKind.Cancelled)
                    {
                        WritePackageLoadFailed(
                            descriptor,
                            loadResult.Error,
                            attempt: 1,
                            elapsedMilliseconds: Stopwatch.GetElapsedTime(loadStartedAt).TotalMilliseconds);
                        WriteCultureSwitchRejected(
                            culture.Name,
                            FormatFallbackCultures(fallbackCultures),
                            loadResult.Error!);
                    }

                    return LocalizationResult.Failed(loadResult.Error!);
                }

                pendingPackages.Add(loadResult.Package!);
            }

            if (IsDisposed)
            {
                DisposeUncachedPackages(pendingPackages);
                return ServiceDisposedResult();
            }

            foreach (var package in pendingPackages)
            {
                var missingCriticalKey = package.Descriptor.CriticalResourceKeys
                    .FirstOrDefault(key => !package.TryGetString(key, out _));
                if (missingCriticalKey is null)
                {
                    continue;
                }

                DisposeUncachedPackages(pendingPackages);
                var error = new LocalizationError(
                    LocalizationErrorKind.InvalidResource,
                    $"Critical localized resource '{missingCriticalKey}' is missing from package '{package.Descriptor.PackageId}'.");
                WritePackageLoadFailed(package.Descriptor, error);
                WriteCultureSwitchRejected(
                    culture.Name,
                    FormatFallbackCultures(fallbackCultures),
                    error);

                return LocalizationResult.Failed(error);
            }

            operationToken.ThrowIfCancellationRequested();

            CultureState nextState;
            var packagesToDispose = new List<LanguagePackage>();
            lock (_packageLoadGate)
            {
                foreach (var package in pendingPackages)
                {
                    if (!IsDescriptorActive(package.Descriptor))
                    {
                        packagesToDispose.Add(package);
                        continue;
                    }

                    var cacheKey = CreatePackageCacheKey(package.Descriptor);
                    if (_loadedPackages.TryGetValue(cacheKey, out var replacedPackage)
                        && !ReferenceEquals(replacedPackage, package))
                    {
                        packagesToDispose.Add(replacedPackage);
                    }

                    _loadedPackages[cacheKey] = package;
                    _packageCacheVersion++;
                }

                var currentState = State;
                nextState = new CultureState(
                    culture,
                    culture,
                    fallbackCultures,
                    currentState.Revision + 1,
                    GetLoadedPackageIdsUnsafe(culture, fallbackCultures));
            }

            SetState(nextState);
            SynchronizeLoadedPackageState();
            DisposeAll(packagesToDispose);
            WriteDiagnostic(
                LocalizationDiagnosticIds.CultureChanged,
                $"Culture changed to '{culture.Name}'.",
                LocalizationDiagnosticSeverity.Info,
                cultureName: culture.Name,
                fallbackCultureName: FormatFallbackCultures(nextState.FallbackCultures));

            // The state commit is the cancellation boundary. Caller cancellation after this
            // point cannot roll back the published culture, so post-commit work is owned by
            // the service lifetime and must run to completion as one transaction.
            var bridgeResult = await ApplyPresentationCultureAsync(nextState, _lifetimeToken).ConfigureAwait(false);
            if (!bridgeResult.Succeeded)
            {
                WriteDiagnostic(
                    LocalizationDiagnosticIds.AtomUiApplyFailed,
                    bridgeResult.Error!.Message,
                    LocalizationDiagnosticSeverity.Error,
                    cultureName: culture.Name,
                    errorKind: bridgeResult.Error.Kind);
            }

            await RefreshLocalizedTextsAsync(_lifetimeToken).ConfigureAwait(false);

            return bridgeResult.Succeeded ? LocalizationResult.Success() : bridgeResult;
        }
        catch (OperationCanceledException)
        {
            return LocalizationResult.Failed(
                new LocalizationError(LocalizationErrorKind.Cancelled, "Culture switch was cancelled."));
        }
    }

    /// <summary>
    /// Executes the get string async operation.
    /// </summary>
    public async ValueTask<LocalizedString> GetStringAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        return await GetStringAsync(key, LocalizationLookupContext.Global, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the get string async operation.
    /// </summary>
    public async ValueTask<LocalizedString> GetStringAsync(
        string key,
        LocalizationLookupContext context,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var stateSnapshot = State;
        var descriptorSnapshot = GetDescriptorSnapshot();
        var activeScopes = GetActiveScopeSnapshot();
        var operationId = Guid.NewGuid().ToString("N");
        var visibleDescriptors = GetVisibleDescriptorsForLookup(
                descriptorSnapshot,
                context,
                activeScopes)
            .ToArray();
        var fallbackCultures = CreateFallbackCultures(
            visibleDescriptors,
            stateSnapshot.CurrentCulture,
            out var rejectedFallbackCulture);

        if (rejectedFallbackCulture is not null)
        {
            WriteDiagnostic(
                LocalizationDiagnosticIds.FallbackMissing,
                $"Localization fallback graph contains a cycle at culture '{rejectedFallbackCulture.Name}'.",
                LocalizationDiagnosticSeverity.Warning,
                cultureName: stateSnapshot.CurrentCulture.Name,
                fallbackCultureName: rejectedFallbackCulture.Name,
                resourceKey: key,
                errorKind: LocalizationErrorKind.InvalidCulture,
                operationId: operationId);
        }

        foreach (var descriptor in GetDescriptors(visibleDescriptors, stateSnapshot.CurrentCulture))
        {
            var loadStartedAt = Stopwatch.GetTimestamp();
            var loadResult = await LoadPackageAsync(descriptor, cache: true, cancellationToken).ConfigureAwait(false);
            if (loadResult.Succeeded && loadResult.Package!.TryGetString(key, out var value))
            {
                return LocalizedString.Found(key, value, descriptor.Culture);
            }

            if (!loadResult.Succeeded)
            {
                if (loadResult.Error?.Kind == LocalizationErrorKind.ServiceDisposed)
                {
                    ThrowIfDisposed();
                }

                if (loadResult.Error?.Kind == LocalizationErrorKind.Cancelled
                    && cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                WritePackageLoadFailed(
                    descriptor,
                    loadResult.Error,
                    operationId,
                    attempt: 1,
                    elapsedMilliseconds: Stopwatch.GetElapsedTime(loadStartedAt).TotalMilliseconds);
            }
        }

        foreach (var fallbackCulture in fallbackCultures)
        {
            foreach (var descriptor in GetDescriptors(visibleDescriptors, fallbackCulture))
            {
                var loadStartedAt = Stopwatch.GetTimestamp();
                var loadResult = await LoadPackageAsync(descriptor, cache: true, cancellationToken).ConfigureAwait(false);
                if (loadResult.Succeeded && loadResult.Package!.TryGetString(key, out var value))
                {
                    return LocalizedString.Fallback(key, value, descriptor.Culture);
                }

                if (!loadResult.Succeeded)
                {
                    if (loadResult.Error?.Kind == LocalizationErrorKind.ServiceDisposed)
                    {
                        ThrowIfDisposed();
                    }

                    if (loadResult.Error?.Kind == LocalizationErrorKind.Cancelled
                        && cancellationToken.IsCancellationRequested)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    WritePackageLoadFailed(
                        descriptor,
                        loadResult.Error,
                        operationId,
                        attempt: 1,
                        elapsedMilliseconds: Stopwatch.GetElapsedTime(loadStartedAt).TotalMilliseconds);
                }
            }
        }

        if (fallbackCultures.Count > 0)
        {
            WriteDiagnostic(
                LocalizationDiagnosticIds.FallbackMissing,
                $"Localized resource '{key}' was not found in the fallback culture chain.",
                LocalizationDiagnosticSeverity.Warning,
                cultureName: stateSnapshot.CurrentCulture.Name,
                fallbackCultureName: FormatFallbackCultures(fallbackCultures),
                resourceKey: key,
                errorKind: LocalizationErrorKind.ResourceMissing,
                operationId: operationId);
        }

        WriteDiagnostic(
            LocalizationDiagnosticIds.ResourceMissing,
            $"Localized resource '{key}' was not found.",
            LocalizationDiagnosticSeverity.Warning,
            cultureName: stateSnapshot.CurrentCulture.Name,
            resourceKey: key,
            errorKind: LocalizationErrorKind.ResourceMissing,
            operationId: operationId);

        return LocalizedString.Missing(key, stateSnapshot.CurrentCulture);
    }

    /// <summary>
    /// Executes the get message async operation.
    /// </summary>
    public async ValueTask<LocalizedMessage> GetMessageAsync(
        string key,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default)
    {
        return await GetMessageAsync(
                key,
                arguments,
                LocalizationLookupContext.Global,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the get message async operation.
    /// </summary>
    public async ValueTask<LocalizedMessage> GetMessageAsync(
        string key,
        IReadOnlyList<object?> arguments,
        LocalizationLookupContext context,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(context);

        var template = await GetStringAsync(key, context, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (template.IsMissing || arguments.Count == 0)
        {
            return LocalizedMessage.FromString(template, template.Value);
        }

        try
        {
            return LocalizedMessage.FromString(
                template,
                string.Format(template.Culture, template.Value, arguments.ToArray()));
        }
        catch (Exception exception)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteDiagnostic(
                LocalizationDiagnosticIds.MessageFormatFailed,
                exception.Message,
                LocalizationDiagnosticSeverity.Error,
                cultureName: template.Culture.Name,
                resourceKey: key,
                errorKind: LocalizationErrorKind.FormatFailed);

            return LocalizedMessage.FromString(template, template.Value, isFormatFailed: true);
        }
    }

    /// <summary>
    /// Executes the create text async operation.
    /// </summary>
    public async ValueTask<ILocalizedText> CreateTextAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        return await CreateTextAsync(key, LocalizationLookupContext.Global, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the create text async operation.
    /// </summary>
    public async ValueTask<ILocalizedText> CreateTextAsync(
        string key,
        LocalizationLookupContext context,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);

        var text = await LocalizedText.CreateAsync(this, key, context, cancellationToken).ConfigureAwait(false);
        RegisterLocalizedText(text);

        return text;
    }

    /// <summary>
    /// Executes the create message text async operation.
    /// </summary>
    public async ValueTask<ILocalizedText> CreateMessageTextAsync(
        string key,
        IReadOnlyList<object?> arguments,
        CancellationToken cancellationToken = default)
    {
        return await CreateMessageTextAsync(
                key,
                arguments,
                LocalizationLookupContext.Global,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Executes the create message text async operation.
    /// </summary>
    public async ValueTask<ILocalizedText> CreateMessageTextAsync(
        string key,
        IReadOnlyList<object?> arguments,
        LocalizationLookupContext context,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(context);

        var text = await LocalizedMessageText.CreateAsync(
                this,
                key,
                arguments,
                context,
                cancellationToken)
            .ConfigureAwait(false);
        RegisterLocalizedText(text);

        return text;
    }

    /// <summary>
    /// Executes the revoke packages by contribution id async operation.
    /// </summary>
    public ValueTask<int> RevokePackagesByContributionIdAsync(
        string contributionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        ThrowIfDisposed();

        if (CurrentMutation.Value is { IsActive: true } currentMutation
            && ReferenceEquals(currentMutation.Owner, this))
        {
            return ValueTask.FromException<int>(
                new InvalidOperationException(
                    "A localization mutation cannot be started from the active localization mutation callback."));
        }

        return new ValueTask<int>(
            EnqueueMutationAsync(() => RevokePackagesByContributionIdCoreAsync(
                contributionId,
                cancellationToken)));
    }

    private async Task<int> RevokePackagesByContributionIdCoreAsync(
        string contributionId,
        CancellationToken cancellationToken)
    {
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeToken);
        var operationToken = operationCancellation.Token;
        operationToken.ThrowIfCancellationRequested();

        var revokedDescriptors = _registry.RevokeContribution(contributionId);
        if (revokedDescriptors.Count == 0)
        {
            return 0;
        }

        var revokedCount = revokedDescriptors.Count;
        var nextState = State;

        WriteDiagnostic(
            LocalizationDiagnosticIds.PluginPackagesRevoked,
            $"Localization packages for contribution '{contributionId}' were revoked.",
            LocalizationDiagnosticSeverity.Info,
            cultureName: nextState.CurrentCulture.Name,
            errorKind: LocalizationErrorKind.ResourceRevoked,
            contributionId: contributionId,
            revokedPackageCount: revokedCount);

        // Revocation is irreversible once descriptors leave the registry. Finish the
        // corresponding bridge/text refresh under the service lifetime, even if the
        // initiating caller cancels after the commit point.
        var bridgeResult = await ApplyPresentationCultureAsync(nextState, _lifetimeToken).ConfigureAwait(false);
        if (!bridgeResult.Succeeded)
        {
            WriteDiagnostic(
                LocalizationDiagnosticIds.AtomUiApplyFailed,
                bridgeResult.Error!.Message,
                LocalizationDiagnosticSeverity.Error,
                cultureName: nextState.CurrentCulture.Name,
                errorKind: bridgeResult.Error.Kind);
        }

        await RefreshLocalizedTextsAsync(_lifetimeToken).ConfigureAwait(false);

        return revokedCount;
    }

    internal void UnregisterLocalizedText(ILocalizedText text)
    {
        lock (_localizedTextGate)
        {
            _localizedTexts.Remove(text);
        }
    }

    internal void DeactivateScope(LocalizationLookupContext context)
    {
        lock (_scopeGate)
        {
            foreach (var key in context.GetScopeKeys())
            {
                if (!_activeScopes.TryGetValue(key, out var referenceCount))
                {
                    continue;
                }

                if (referenceCount == 1)
                {
                    _activeScopes.Remove(key);
                }
                else
                {
                    _activeScopes[key] = referenceCount - 1;
                }
            }
        }
    }

    internal void WriteTextRefreshFailed(string key, Exception exception)
    {
        WriteDiagnostic(
            LocalizationDiagnosticIds.TextRefreshFailed,
            exception.Message,
            LocalizationDiagnosticSeverity.Error,
            cultureName: CurrentCulture.Name,
            resourceKey: key,
            errorKind: LocalizationErrorKind.RefreshFailed);
    }

    private void RegisterLocalizedText(ILocalizedText text)
    {
        var dispose = false;
        lock (_localizedTextGate)
        {
            if (IsDisposed)
            {
                dispose = true;
            }
            else
            {
                _localizedTexts.Add(text);
            }
        }

        if (dispose)
        {
            text.Dispose();
            ThrowIfDisposed();
        }
    }

    private async ValueTask RefreshLocalizedTextsAsync(CancellationToken cancellationToken)
    {
        ILocalizedText[] localizedTexts;

        lock (_localizedTextGate)
        {
            localizedTexts = _localizedTexts.ToArray();
        }

        foreach (var localizedText in localizedTexts)
        {
            try
            {
                await localizedText.RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                WriteTextRefreshFailed(localizedText.Key, exception);
            }
        }
    }

    private async ValueTask<LanguagePackageLoadResult> LoadPackageAsync(
        LanguagePackageDescriptor descriptor,
        bool cache,
        CancellationToken cancellationToken)
    {
        if (IsDisposed)
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.ServiceDisposed,
                    "Localization service has been disposed."));
        }

        if (!cache)
        {
            lock (_packageLoadGate)
            {
                if (_loadedPackages.TryGetValue(CreatePackageCacheKey(descriptor), out var loadedPackage))
                {
                    return LanguagePackageLoadResult.Success(loadedPackage);
                }
            }

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeToken);

            return await LoadPackageCoreAsync(descriptor, linkedCancellation.Token).ConfigureAwait(false);
        }

        var cacheKey = CreatePackageCacheKey(descriptor);
        Task<LanguagePackageLoadResult> loadTask;
        lock (_packageLoadGate)
        {
            if (IsDisposed)
            {
                return LanguagePackageLoadResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.ServiceDisposed,
                        "Localization service has been disposed."));
            }

            if (_loadedPackages.TryGetValue(cacheKey, out var loadedPackage))
            {
                return LanguagePackageLoadResult.Success(loadedPackage);
            }

            if (!_packageLoadTasks.TryGetValue(cacheKey, out loadTask!))
            {
                loadTask = LoadAndCachePackageAsync(descriptor, cacheKey);
                _packageLoadTasks[cacheKey] = loadTask;
            }
        }

        try
        {
            return await loadTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            return CancelledLoadResult(cancellationToken, exception);
        }
    }

    private async Task<LanguagePackageLoadResult> LoadAndCachePackageAsync(
        LanguagePackageDescriptor descriptor,
        (string CultureName, string PackageId) cacheKey)
    {
        await Task.Yield();

        var loadResult = await LoadPackageCoreAsync(
                descriptor,
                _lifetimeToken)
            .ConfigureAwait(false);
        LanguagePackage? packageToDispose = null;
        var packagePublished = false;

        lock (_packageLoadGate)
        {
            _packageLoadTasks.Remove(cacheKey);
            if (loadResult.Succeeded)
            {
                if (IsDisposed || !IsDescriptorActive(descriptor))
                {
                    packageToDispose = loadResult.Package;
                    loadResult = IsDisposed
                        ? LanguagePackageLoadResult.Failed(
                            new LocalizationError(
                                LocalizationErrorKind.ServiceDisposed,
                                "Localization service has been disposed."))
                        : LanguagePackageLoadResult.Failed(
                            new LocalizationError(
                                LocalizationErrorKind.ResourceRevoked,
                                $"Language package '{descriptor.PackageId}' was revoked while loading."));
                }
                else
                {
                    if (_loadedPackages.TryGetValue(cacheKey, out var replacedPackage)
                        && !ReferenceEquals(replacedPackage, loadResult.Package))
                    {
                        packageToDispose = replacedPackage;
                    }

                    _loadedPackages[cacheKey] = loadResult.Package!;
                    _packageCacheVersion++;
                    packagePublished = true;
                }
            }
        }

        packageToDispose?.Dispose();
        if (packagePublished)
        {
            SynchronizeLoadedPackageState();
        }

        return loadResult;
    }

    private async ValueTask<LanguagePackageLoadResult> LoadPackageCoreAsync(
        LanguagePackageDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        if (!_providers.TryGetValue(descriptor.ProviderKind, out var provider))
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.PackageNotFound,
                    $"No language package provider is registered for '{descriptor.ProviderKind}'."));
        }

        try
        {
            return await provider.LoadAsync(descriptor, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception)
        {
            return CancelledLoadResult(cancellationToken, exception);
        }
        catch (Exception exception)
        {
            return LanguagePackageLoadResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.PackageLoadFailed,
                    exception.Message,
                    exception));
        }
    }

    private async ValueTask<LocalizationResult> ApplyPresentationCultureAsync(
        CultureState state,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _bridge.ApplyCultureAsync(state, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return LocalizationResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.PresentationApplyFailed,
                    exception.Message,
                    exception));
        }
    }

    private static (string CultureName, string PackageId) CreatePackageCacheKey(
        LanguagePackageDescriptor descriptor)
    {
        return (descriptor.Culture.Name, descriptor.PackageId);
    }

    private LanguagePackageDescriptor[] GetDescriptorSnapshot()
    {
        return _registry.Descriptors.ToArray();
    }

    private bool IsDescriptorActive(LanguagePackageDescriptor descriptor)
    {
        return _registry.Contains(descriptor);
    }

    private HashSet<LocalizationScopeKey> GetActiveScopeSnapshot()
    {
        lock (_scopeGate)
        {
            return _activeScopes.Keys.ToHashSet();
        }
    }

    private LanguagePackageDescriptor[] GetActiveDescriptors(
        IEnumerable<LanguagePackageDescriptor> descriptors,
        CultureInfo culture)
    {
        return GetDescriptors(GetAllActiveDescriptors(descriptors), culture).ToArray();
    }

    private LanguagePackageDescriptor[] GetAllActiveDescriptors(
        IEnumerable<LanguagePackageDescriptor> descriptors)
    {
        var activeScopes = GetActiveScopeSnapshot();

        return descriptors
            .Where(descriptor => IsGlobalScope(descriptor.Scope)
                || activeScopes.Contains(new LocalizationScopeKey(descriptor.Scope, descriptor.ScopeId!)))
            .ToArray();
    }

    private static IEnumerable<LanguagePackageDescriptor> GetVisibleDescriptorsForLookup(
        IEnumerable<LanguagePackageDescriptor> descriptors,
        LocalizationLookupContext context,
        IReadOnlySet<LocalizationScopeKey> activeScopes)
    {
        return descriptors.Where(descriptor => IsGlobalScope(descriptor.Scope)
                || (context.Matches(descriptor)
                    && activeScopes.Contains(new LocalizationScopeKey(descriptor.Scope, descriptor.ScopeId!))));
    }

    private static bool IsGlobalScope(ResourceScope scope)
    {
        return scope is ResourceScope.Host or ResourceScope.Presentation;
    }

    private void OnDescriptorsRevoked(IReadOnlyList<LanguagePackageDescriptor> revokedDescriptors)
    {
        var revokedCacheKeys = revokedDescriptors
            .Select(CreatePackageCacheKey)
            .ToHashSet();
        LanguagePackage[] packagesToDispose;

        lock (_packageLoadGate)
        {
            packagesToDispose = _loadedPackages
                .Where(pair => revokedCacheKeys.Contains(pair.Key))
                .Select(pair => pair.Value)
                .ToArray();

            foreach (var cacheKey in revokedCacheKeys)
            {
                _loadedPackages.Remove(cacheKey);
                _packageLoadTasks.Remove(cacheKey);
            }
            _packageCacheVersion++;
        }

        SynchronizeLoadedPackageState();
        DisposeAll(packagesToDispose);
    }

    private static IEnumerable<LanguagePackageDescriptor> GetDescriptors(
        IEnumerable<LanguagePackageDescriptor> descriptors,
        CultureInfo culture)
    {
        return descriptors.Where(descriptor =>
                string.Equals(descriptor.Culture.Name, culture.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(descriptor => GetScopeLookupRank(descriptor.Scope));
    }

    private static int GetScopeLookupRank(ResourceScope scope)
    {
        return scope switch
        {
            ResourceScope.Route => 0,
            ResourceScope.Window => 1,
            ResourceScope.Plugin => 2,
            ResourceScope.Module => 3,
            ResourceScope.Host => 4,
            ResourceScope.Presentation => 5,
            _ => int.MaxValue,
        };
    }

    private IReadOnlyList<CultureInfo> CreateFallbackCultures(
        IEnumerable<LanguagePackageDescriptor> descriptors,
        CultureInfo culture,
        out CultureInfo? rejectedFallbackCulture)
    {
        rejectedFallbackCulture = null;
        CultureInfo? rejected = null;
        var fallbackCultures = new List<CultureInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            culture.Name,
        };
        var activePath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool Visit(CultureInfo source, bool includeConfiguredFallbacks)
        {
            activePath.Add(source.Name);
            foreach (var fallbackCulture in GetDirectFallbackCultures(
                         descriptors,
                         source,
                         includeConfiguredFallbacks))
            {
                if (activePath.Contains(fallbackCulture.Name))
                {
                    rejected = fallbackCulture;
                    activePath.Remove(source.Name);

                    return false;
                }

                if (seen.Add(fallbackCulture.Name))
                {
                    fallbackCultures.Add(fallbackCulture);
                    if (!Visit(fallbackCulture, includeConfiguredFallbacks: false))
                    {
                        activePath.Remove(source.Name);

                        return false;
                    }
                }
            }

            activePath.Remove(source.Name);

            return true;
        }

        if (!Visit(culture, includeConfiguredFallbacks: true))
        {
            rejectedFallbackCulture = rejected;
            return fallbackCultures;
        }

        var parent = culture.Parent;
        while (!string.IsNullOrEmpty(parent.Name))
        {
            if (seen.Add(parent.Name))
            {
                fallbackCultures.Add(parent);
            }

            parent = parent.Parent;
        }

        if (seen.Add(CultureInfo.InvariantCulture.Name))
        {
            fallbackCultures.Add(CultureInfo.InvariantCulture);
        }

        return fallbackCultures;
    }

    private IEnumerable<CultureInfo> GetDirectFallbackCultures(
        IEnumerable<LanguagePackageDescriptor> descriptors,
        CultureInfo culture,
        bool includeConfiguredFallbacks)
    {
        foreach (var fallbackCulture in GetDescriptors(descriptors, culture)
                     .Select(descriptor => descriptor.FallbackCulture)
                     .Where(fallbackCulture => fallbackCulture is not null)
                     .Cast<CultureInfo>())
        {
            yield return fallbackCulture;
        }

        if (!includeConfiguredFallbacks)
        {
            yield break;
        }

        foreach (var fallbackCulture in _configuredFallbackCultures)
        {
            yield return fallbackCulture;
        }
    }

    private static bool TryGetCulture(
        string cultureName,
        out CultureInfo culture,
        out LocalizationError? error)
    {
        try
        {
            culture = CultureInfo.GetCultureInfo(cultureName);
            error = null;

            return true;
        }
        catch (CultureNotFoundException exception)
        {
            culture = CultureInfo.InvariantCulture;
            error = new LocalizationError(
                LocalizationErrorKind.InvalidCulture,
                $"Culture '{cultureName}' is not supported.",
                exception);

            return false;
        }
    }

    private static string FormatFallbackCultures(IEnumerable<CultureInfo> fallbackCultures)
    {
        return string.Join(";", fallbackCultures.Select(culture => culture.Name));
    }

    private static IReadOnlyList<LanguagePackageDescriptor> GetLanguagePackageDescriptors(LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.LanguagePackages.ToArray();
    }

    private static IReadOnlyDictionary<LanguagePackageProviderKind, ILanguagePackageProvider> BuildProviderMap(
        IEnumerable<ILanguagePackageProvider> providers)
    {
        var providerArray = providers.ToArray();
        if (providerArray.Any(provider => provider is null))
        {
            throw new ArgumentException(
                "Language package providers cannot contain null values.",
                nameof(providers));
        }

        var invalidProvider = providerArray.FirstOrDefault(provider => !Enum.IsDefined(provider.Kind));
        if (invalidProvider is not null)
        {
            throw new ArgumentException(
                $"Language package provider '{invalidProvider.GetType().FullName}' declares unknown kind '{invalidProvider.Kind}'.",
                nameof(providers));
        }

        var map = new Dictionary<LanguagePackageProviderKind, ILanguagePackageProvider>();
        foreach (var group in providerArray.GroupBy(provider => provider.Kind))
        {
            var customProviders = group.Where(provider => !IsBuiltInProvider(provider)).ToArray();
            if (customProviders.Length > 1)
            {
                throw new ArgumentException(
                    $"More than one custom language package provider is registered for '{group.Key}'.",
                    nameof(providers));
            }

            map.Add(group.Key, customProviders.SingleOrDefault() ?? group.First());
        }

        return map;
    }

    private static bool IsBuiltInProvider(ILanguagePackageProvider provider)
    {
        return provider.GetType() == typeof(FileLanguagePackageProvider)
            || provider.GetType() == typeof(AssemblyLanguagePackageProvider)
            || provider.GetType() == typeof(InMemoryLanguagePackageProvider);
    }

    private static CultureInfo GetDefaultCulture(LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.DefaultCulture;
    }

    private static CultureInfo GetDefaultUICulture(LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.DefaultUICulture;
    }

    private static IReadOnlyList<CultureInfo> GetConfiguredFallbackCultures(LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.FallbackCultures.ToArray();
    }

    private void WritePackageLoadFailed(
        LanguagePackageDescriptor descriptor,
        LocalizationError? error,
        string? operationId = null,
        int? attempt = null,
        double? elapsedMilliseconds = null)
    {
        WriteDiagnostic(
            LocalizationDiagnosticIds.PackageLoadFailed,
            error?.Message ?? $"Language package '{descriptor.PackageId}' failed to load.",
            LocalizationDiagnosticSeverity.Error,
            cultureName: descriptor.Culture.Name,
            packageId: descriptor.PackageId,
            scope: descriptor.Scope,
            errorKind: error?.Kind,
            scopeId: descriptor.ScopeId,
            providerKind: descriptor.ProviderKind,
            location: descriptor.Location,
            operationId: operationId,
            attempt: attempt,
            elapsedMilliseconds: elapsedMilliseconds);
    }

    private void WriteDiagnostic(
        string code,
        string message,
        LocalizationDiagnosticSeverity severity,
        string? cultureName = null,
        string? fallbackCultureName = null,
        string? resourceKey = null,
        string? packageId = null,
        ResourceScope? scope = null,
        LocalizationErrorKind? errorKind = null,
        string? contributionId = null,
        int? revokedPackageCount = null,
        string? scopeId = null,
        LanguagePackageProviderKind? providerKind = null,
        string? location = null,
        string? operationId = null,
        int? attempt = null,
        double? elapsedMilliseconds = null)
    {
        try
        {
            _diagnostics?.Write(
                new LocalizationDiagnosticRecord(
                    code,
                    message,
                    severity,
                    CultureName: cultureName,
                    FallbackCultureName: fallbackCultureName,
                    ResourceKey: resourceKey,
                    PackageId: packageId,
                    Scope: scope,
                    CultureRevision: State.Revision,
                    ErrorKind: errorKind,
                    ContributionId: contributionId,
                    RevokedPackageCount: revokedPackageCount,
                    ScopeId: scopeId,
                    ProviderKind: providerKind,
                    Location: location,
                    OperationId: operationId
                        ?? (CurrentMutation.Value is { IsActive: true } mutation
                            ? mutation.OperationId
                            : Guid.NewGuid().ToString("N")),
                    Attempt: attempt,
                    ElapsedMilliseconds: elapsedMilliseconds));
        }
        catch
        {
            // Diagnostics are observational and must never alter localization behavior.
        }
    }

    private void WriteCultureSwitchRejected(
        string cultureName,
        string? fallbackCultureName,
        LocalizationError error)
    {
        WriteDiagnostic(
            LocalizationDiagnosticIds.CultureSwitchRejected,
            error.Message,
            LocalizationDiagnosticSeverity.Warning,
            cultureName: cultureName,
            fallbackCultureName: fallbackCultureName,
            errorKind: error.Kind);
    }

    private static void DisposeAll(IEnumerable<LanguagePackage> packages)
    {
        foreach (var package in packages)
        {
            package.Dispose();
        }
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        ThrowIfReentrantDispose();
        GetOrStartDisposeTask().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Executes the dispose async operation.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        ThrowIfReentrantDispose();
        return new ValueTask(GetOrStartDisposeTask());
    }

    private Task GetOrStartDisposeTask()
    {
        TaskCompletionSource? completion = null;
        Task disposeTask;
        lock (_disposeGate)
        {
            if (_disposeTask is not null)
            {
                return _disposeTask;
            }

            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            disposeTask = completion.Task;
            _disposeTask = disposeTask;
            Volatile.Write(ref _disposed, 1);
        }

        _ = RunDisposeAsync(completion);
        return disposeTask;
    }

    private async Task RunDisposeAsync(TaskCompletionSource completion)
    {
        Exception? disposeError = null;
        try
        {
            _registry.DescriptorsRevoked -= OnDescriptorsRevoked;
            _lifetimeCancellation.Cancel();

            Task mutationTail;
            lock (_mutationQueueGate)
            {
                mutationTail = _mutationTail;
            }

            try
            {
                await mutationTail.ConfigureAwait(false);
            }
            catch
            {
            }

            Task[] packageLoads;
            lock (_packageLoadGate)
            {
                packageLoads = _packageLoadTasks.Values.ToArray();
            }

            try
            {
                await Task.WhenAll(packageLoads).ConfigureAwait(false);
            }
            catch
            {
            }

            CompleteDispose();
            GC.SuppressFinalize(this);
        }
        catch (Exception exception)
        {
            disposeError = exception;
        }

        if (disposeError is null)
        {
            completion.TrySetResult();
        }
        else
        {
            completion.TrySetException(disposeError);
        }
    }

    private void ThrowIfReentrantDispose()
    {
        if (CurrentMutation.Value is { IsActive: true } mutation
            && ReferenceEquals(mutation.Owner, this))
        {
            throw new InvalidOperationException(
                "Localization service disposal cannot run inside an active localization mutation callback.");
        }
    }

    private void CompleteDispose()
    {
        ILocalizedText[] localizedTexts;
        LanguagePackage[] packages;

        lock (_localizedTextGate)
        {
            localizedTexts = _localizedTexts.ToArray();
            _localizedTexts.Clear();
        }

        lock (_packageLoadGate)
        {
            packages = _loadedPackages.Values.Distinct().ToArray();
            _loadedPackages.Clear();
            _packageLoadTasks.Clear();
        }

        foreach (var text in localizedTexts)
        {
            text.Dispose();
        }

        DisposeAll(packages);
        _cultureState.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private void DisposeUncachedPackages(IEnumerable<LanguagePackage> packages)
    {
        LanguagePackage[] uncached;
        lock (_packageLoadGate)
        {
            uncached = packages
                .Where(package => !_loadedPackages.TryGetValue(
                        CreatePackageCacheKey(package.Descriptor),
                        out var cached)
                    || !ReferenceEquals(cached, package))
                .ToArray();
        }

        DisposeAll(uncached);
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }

    private static LocalizationResult ServiceDisposedResult()
    {
        return LocalizationResult.Failed(
            new LocalizationError(
                LocalizationErrorKind.ServiceDisposed,
                "Localization service has been disposed."));
    }

    private static LanguagePackageLoadResult CancelledLoadResult(
        CancellationToken cancellationToken,
        OperationCanceledException exception)
    {
        return LanguagePackageLoadResult.Failed(
            new LocalizationError(
                LocalizationErrorKind.Cancelled,
                "Language package load was cancelled.",
                exception.CancellationToken == cancellationToken
                    ? exception
                    : new OperationCanceledException(exception.Message, exception, cancellationToken)));
    }

    private void SetState(CultureState state)
    {
        _cultureState.SetValue(state);
    }

    private void SynchronizeLoadedPackageState()
    {
        while (!IsDisposed)
        {
            var current = State;
            long cacheVersion;
            IReadOnlyList<string> loadedPackageIds;
            lock (_packageLoadGate)
            {
                cacheVersion = _packageCacheVersion;
                loadedPackageIds = GetLoadedPackageIdsUnsafe(
                    current.CurrentCulture,
                    current.FallbackCultures);
            }

            if (!current.LoadedPackageIds.SequenceEqual(loadedPackageIds, StringComparer.Ordinal))
            {
                SetState(new CultureState(
                    current.CurrentCulture,
                    current.CurrentUICulture,
                    current.FallbackCultures,
                    current.Revision + 1,
                    loadedPackageIds));
            }

            if (cacheVersion == Interlocked.Read(ref _packageCacheVersion))
            {
                return;
            }
        }
    }

    private IReadOnlyList<string> GetLoadedPackageIdsUnsafe(
        CultureInfo currentCulture,
        IReadOnlyList<CultureInfo> fallbackCultures)
    {
        var packageIds = new List<string>();
        var seenPackageIds = new HashSet<string>(StringComparer.Ordinal);

        AddCulturePackages(currentCulture);
        foreach (var fallbackCulture in fallbackCultures)
        {
            AddCulturePackages(fallbackCulture);
        }

        return packageIds;

        void AddCulturePackages(CultureInfo culture)
        {
            foreach (var package in _loadedPackages.Values.Where(package => string.Equals(
                         package.Descriptor.Culture.Name,
                         culture.Name,
                         StringComparison.OrdinalIgnoreCase)))
            {
                if (seenPackageIds.Add(package.Descriptor.PackageId))
                {
                    packageIds.Add(package.Descriptor.PackageId);
                }
            }
        }
    }

    private Task<T> EnqueueMutationAsync<T>(Func<Task<T>> mutation)
    {
        Task<T> queued;
        lock (_mutationQueueGate)
        {
            if (IsDisposed)
            {
                return Task.FromException<T>(new ObjectDisposedException(nameof(LocalizationService)));
            }

            var predecessor = _mutationTail;
            queued = RunMutationAfterAsync(predecessor, mutation);
            _mutationTail = queued;
        }

        return queued;
    }

    private async Task<T> RunMutationAfterAsync<T>(
        Task predecessor,
        Func<Task<T>> mutation)
    {
        await Task.Yield();

        try
        {
            await predecessor.ConfigureAwait(false);
        }
        catch
        {
            // Every queued caller observes its own failure; later mutations must still run.
        }

        var previous = CurrentMutation.Value;
        var execution = new MutationExecution(this);
        CurrentMutation.Value = execution;
        try
        {
            return await mutation().ConfigureAwait(false);
        }
        finally
        {
            execution.Deactivate();
            CurrentMutation.Value = previous;
        }
    }

    private sealed class MutationExecution(LocalizationService owner)
    {
        private int _active = 1;

        public LocalizationService Owner { get; } = owner;

        public string OperationId { get; } = Guid.NewGuid().ToString("N");

        public bool IsActive => Volatile.Read(ref _active) != 0;

        public void Deactivate() => Volatile.Write(ref _active, 0);
    }

    private sealed class NoopPresentationLocalizationBridge : IPresentationLocalizationBridge
    {
        public static readonly NoopPresentationLocalizationBridge Instance = new();

        private NoopPresentationLocalizationBridge()
        {
        }

        public ValueTask<LocalizationResult> ApplyCultureAsync(
            CultureState state,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(LocalizationResult.Success());
        }
    }
}
