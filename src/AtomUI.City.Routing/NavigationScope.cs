using System.Diagnostics;
using System.Runtime.ExceptionServices;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Routing;

/// <summary>
/// Represents navigation scope.
/// </summary>
public sealed class NavigationScope : IRouter, IDisposable, IAsyncDisposable
{
    private const int MaxRedirectCount = 8;
    private const string ComponentFailureRecordedKey = "AtomUI.City.Routing.ComponentFailureRecorded";
    private static readonly AsyncLocal<NavigationExecution?> CurrentExecution = new();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _lifecycleGate = new();
    private readonly CancellationTokenSource _scopeCancellation = new();
    private readonly List<NavigationJournalEntry> _backStack = [];
    private readonly List<NavigationJournalEntry> _forwardStack = [];
    private readonly IRouteGraphProvider _routeGraphProvider;
    private readonly Func<Type, object?> _serviceResolver;
    private readonly IHostDiagnostics? _diagnostics;
    private RouteGraphSnapshot _currentRouteGraph;
    private NavigationSnapshot _currentSnapshot;
    private NavigationJournalEntry? _currentJournalEntry;
    private CancellationTokenSource? _runningNavigationCancellation;
    private long _cancelPreviousVersion;
    private Task? _disposeTask;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <c>NavigationScope</c> type.
    /// </summary>
    public NavigationScope(
        RouteGraphSnapshot routeGraph,
        Func<Type, object?>? serviceResolver = null,
        IHostDiagnostics? diagnostics = null)
        : this(new FixedRouteGraphProvider(routeGraph), serviceResolver, diagnostics)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>NavigationScope</c> type.
    /// </summary>
    public NavigationScope(
        IRouteGraphProvider routeGraphProvider,
        Func<Type, object?>? serviceResolver = null,
        IHostDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(routeGraphProvider);

        _routeGraphProvider = routeGraphProvider;
        _currentRouteGraph = routeGraphProvider.CurrentSnapshot;
        _serviceResolver = serviceResolver ?? (_ => null);
        _diagnostics = diagnostics;
        _currentSnapshot = NavigationSnapshot.Empty(_currentRouteGraph.Version);
    }

    /// <summary>
    /// Gets router.
    /// </summary>
    public IRouter Router => this;

    /// <summary>
    /// Gets current snapshot.
    /// </summary>
    public NavigationSnapshot CurrentSnapshot => Volatile.Read(ref _currentSnapshot);

    /// <summary>
    /// Executes the navigate async operation.
    /// </summary>
    public ValueTask<NavigationResult> NavigateAsync(
        RouteReference route,
        NavigationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var target = NavigationTarget.FromRouteReference(
            route.Id,
            parameters: null,
            options ?? NavigationOptions.Default);

        return NavigateCoreAsync(target, cancellationToken);
    }

    /// <summary>
    /// Executes the navigate async&lt;tparameters&gt; operation.
    /// </summary>
    public ValueTask<NavigationResult> NavigateAsync<TParameters>(
        RouteReference<TParameters> route,
        TParameters parameters,
        NavigationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var target = NavigationTarget.FromRouteReference(
            route.Id,
            route.BindParameters(parameters),
            options ?? NavigationOptions.Default);

        return NavigateCoreAsync(target, cancellationToken);
    }

    /// <summary>
    /// Executes the navigate by path async operation.
    /// </summary>
    public ValueTask<NavigationResult> NavigateByPathAsync(
        string path,
        NavigationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var target = NavigationTarget.FromPath(path, options ?? NavigationOptions.Default);

        return NavigateCoreAsync(target, cancellationToken);
    }

    /// <summary>
    /// Executes the navigate by uri async operation.
    /// </summary>
    public ValueTask<NavigationResult> NavigateByUriAsync(
        Uri uri,
        NavigationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var target = NavigationTarget.FromDeepLink(uri, options ?? NavigationOptions.Default);
        return NavigateCoreAsync(target, cancellationToken);
    }

    /// <summary>
    /// Executes the back async operation.
    /// </summary>
    public async ValueTask<NavigationResult> BackAsync(CancellationToken cancellationToken = default)
    {
        var journalTarget = NavigationTarget.FromJournal(
            new NavigationOptions { ConcurrencyPolicy = NavigationConcurrencyPolicy.Queue });

        return await RunSerializedNavigationAsync(
            journalTarget,
            async (navigationId, routeGraph, token) =>
            {
                while (_backStack.Count > 0)
                {
                    var entry = _backStack[^1];
                    var current = _currentJournalEntry;
                    _backStack.RemoveAt(_backStack.Count - 1);
                    NavigationResult result;
                    try
                    {
                        result = await ExecuteNavigationAsync(
                            navigationId,
                            CreateJournalTarget(entry),
                            routeGraph,
                            token);
                    }
                    catch
                    {
                        _backStack.Add(entry);
                        throw;
                    }

                    if (IsCompletedNavigation(result))
                    {
                        if (current is not null)
                        {
                            _forwardStack.Add(current);
                        }

                        _currentJournalEntry = CreateJournalEntry(CurrentSnapshot);
                        return result;
                    }

                    if (!IsStaleContributionEntry(entry, result, routeGraph))
                    {
                        _backStack.Add(entry);
                        return result;
                    }
                }

                return JournalNotAvailable(navigationId, journalTarget);
            },
            cancellationToken);
    }

    /// <summary>
    /// Executes the forward async operation.
    /// </summary>
    public async ValueTask<NavigationResult> ForwardAsync(CancellationToken cancellationToken = default)
    {
        var journalTarget = NavigationTarget.FromJournal(
            new NavigationOptions { ConcurrencyPolicy = NavigationConcurrencyPolicy.Queue });

        return await RunSerializedNavigationAsync(
            journalTarget,
            async (navigationId, routeGraph, token) =>
            {
                while (_forwardStack.Count > 0)
                {
                    var entry = _forwardStack[^1];
                    var current = _currentJournalEntry;
                    _forwardStack.RemoveAt(_forwardStack.Count - 1);
                    NavigationResult result;
                    try
                    {
                        result = await ExecuteNavigationAsync(
                            navigationId,
                            CreateJournalTarget(entry),
                            routeGraph,
                            token);
                    }
                    catch
                    {
                        _forwardStack.Add(entry);
                        throw;
                    }

                    if (IsCompletedNavigation(result))
                    {
                        if (current is not null)
                        {
                            _backStack.Add(current);
                        }

                        _currentJournalEntry = CreateJournalEntry(CurrentSnapshot);
                        return result;
                    }

                    if (!IsStaleContributionEntry(entry, result, routeGraph))
                    {
                        _forwardStack.Add(entry);
                        return result;
                    }
                }

                return JournalNotAvailable(navigationId, journalTarget);
            },
            cancellationToken);
    }

    private async ValueTask<NavigationResult> NavigateCoreAsync(
        NavigationTarget target,
        CancellationToken cancellationToken)
    {
        return await RunSerializedNavigationAsync(
            target,
            (navigationId, routeGraph, token) => ExecuteNavigationAsync(
                navigationId,
                target,
                routeGraph,
                token),
            cancellationToken);
    }

    private async ValueTask<NavigationResult> RunSerializedNavigationAsync(
        NavigationTarget admissionTarget,
        Func<Guid, RouteGraphSnapshot, CancellationToken, ValueTask<NavigationResult>> operation,
        CancellationToken cancellationToken)
    {
        var navigationId = Guid.NewGuid();
        var acquiredGate = false;
        CancellationTokenSource? navigationCancellation = null;
        NavigationExecution? previousExecution = null;
        NavigationExecution? execution = null;
        var diagnosticGraphVersion = _routeGraphProvider.CurrentSnapshot.Version;
        var stopwatch = Stopwatch.StartNew();

        var optionsFailure = ValidateOptions(navigationId, admissionTarget);
        if (optionsFailure is not null)
        {
            return optionsFailure;
        }

        if (IsCurrentExecutionActive())
        {
            return NavigationResult.Rejected(
                navigationId,
                admissionTarget,
                "CITY-NAVIGATION-REENTRANT",
                "Navigation cannot be started recursively from the same navigation execution chain.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cancelPreviousVersion = PrepareCancelPrevious(admissionTarget.Options.ConcurrencyPolicy);
            var rejected = await TryEnterNavigationAsync(
                navigationId,
                admissionTarget,
                cancellationToken);

            if (rejected is not null)
            {
                return rejected;
            }

            acquiredGate = true;
            navigationCancellation = BeginRunningNavigation(cancellationToken, cancelPreviousVersion);
            ApplyTimeout(navigationCancellation, admissionTarget.Options.Timeout);
            cancellationToken = navigationCancellation.Token;
            var routeGraph = _routeGraphProvider.CurrentSnapshot;
            diagnosticGraphVersion = routeGraph.Version;

            cancellationToken.ThrowIfCancellationRequested();
            previousExecution = CurrentExecution.Value;
            execution = new NavigationExecution(this);
            CurrentExecution.Value = execution;
            WriteNavigationStarted(navigationId, admissionTarget, routeGraph.Version);
            var result = await operation(navigationId, routeGraph, cancellationToken);
            WriteNavigationCompleted(result, routeGraph.Version, stopwatch.Elapsed);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var result = NavigationResult.Cancelled(navigationId, admissionTarget);
            WriteNavigationCompleted(result, diagnosticGraphVersion, stopwatch.Elapsed);
            return result;
        }
        catch (Exception exception)
        {
            var result = NavigationResult.Failed(
                navigationId,
                admissionTarget,
                "CITY-NAVIGATION-FAILED",
                exception.Message,
                exception);
            WriteNavigationCompleted(result, diagnosticGraphVersion, stopwatch.Elapsed);
            return result;
        }
        finally
        {
            execution?.Deactivate();
            if (ReferenceEquals(CurrentExecution.Value, execution))
            {
                CurrentExecution.Value = previousExecution;
            }

            if (navigationCancellation is not null)
            {
                EndRunningNavigation(navigationCancellation);
            }

            if (acquiredGate)
            {
                _gate.Release();
            }
        }
    }

    private async ValueTask<NavigationResult> ExecuteNavigationAsync(
        Guid navigationId,
        NavigationTarget initialTarget,
        RouteGraphSnapshot routeGraph,
        CancellationToken cancellationToken)
    {
        var target = initialTarget;
        NavigationTarget? completedRedirectTarget = null;
        var visitedTargets = new HashSet<NavigationTargetIdentity>();

        for (var redirectCount = 0; ; redirectCount++)
        {
            if (!visitedTargets.Add(new NavigationTargetIdentity(target)))
            {
                return NavigationResult.Failed(
                    navigationId,
                    target,
                    "CITY-NAVIGATION-REDIRECT-LOOP",
                    $"Navigation redirect loop detected at '{target}'.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = target.Kind switch
            {
                NavigationTargetKind.Path => await NavigateByMatchedPathAsync(navigationId, target, routeGraph, cancellationToken),
                NavigationTargetKind.DeepLink => await NavigateByMatchedPathAsync(navigationId, target, routeGraph, cancellationToken),
                NavigationTargetKind.RouteReference => await NavigateByRouteIdAsync(navigationId, target, routeGraph, cancellationToken),
                _ => NavigationResult.Rejected(
                    navigationId,
                    target,
                    "CITY-NAVIGATION-TARGET-UNSUPPORTED",
                    $"Navigation target kind '{target.Kind}' is not supported yet."),
            };

            if (result.Status == NavigationResultStatus.Redirected &&
                result.RedirectTarget is not null &&
                result.ActiveRoute is null)
            {
                if (!target.Options.AllowRedirect)
                {
                    return NavigationResult.Rejected(
                        navigationId,
                        target,
                        "CITY-NAVIGATION-REDIRECT-DISABLED",
                        "Navigation redirects are disabled for this navigation target.");
                }

                if (redirectCount >= MaxRedirectCount)
                {
                    return NavigationResult.Failed(
                        navigationId,
                        target,
                        "CITY-NAVIGATION-REDIRECT-LIMIT",
                        $"Navigation exceeded the redirect limit of {MaxRedirectCount}.");
                }

                target = result.RedirectTarget.InheritRedirectContextFrom(target);
                completedRedirectTarget = target;
                continue;
            }

            if (completedRedirectTarget is not null && result.Status == NavigationResultStatus.Success)
            {
                return NavigationResult.Redirected(
                    navigationId,
                    initialTarget,
                    completedRedirectTarget,
                    result.Route,
                    result.Parameters);
            }

            return result;
        }
    }

    private async ValueTask<NavigationResult> NavigateByMatchedPathAsync(
        Guid navigationId,
        NavigationTarget target,
        RouteGraphSnapshot routeGraph,
        CancellationToken cancellationToken)
    {
        foreach (var match in routeGraph.Matcher.MatchAll(target.Path!, target.Options.OutletName))
        {
            if (!await CanMatchAsync(navigationId, target, match.Route, cancellationToken))
            {
                continue;
            }

            if (match.Route.Kind == RouteDefinitionKind.Redirect)
            {
                return CreateStaticRedirect(
                    navigationId,
                    target,
                    match.Route,
                    MergeParameters(target.Parameters, match.Parameters));
            }

            var parameters = new Dictionary<string, string>(target.Parameters, StringComparer.OrdinalIgnoreCase);
            foreach (var item in match.Parameters)
            {
                parameters[item.Key] = item.Value;
            }

            return await CompleteNavigationAsync(
                navigationId,
                target,
                match.Route,
                parameters,
                routeGraph,
                cancellationToken);
        }

        return NavigationResult.NotFound(
            navigationId,
            target,
            $"No route matched path '{target.Path}'.");
    }

    private async ValueTask<NavigationResult> NavigateByRouteIdAsync(
        Guid navigationId,
        NavigationTarget target,
        RouteGraphSnapshot routeGraph,
        CancellationToken cancellationToken)
    {
        if (!routeGraph.TryGetRoute(target.RouteId!, out var route) || route is null)
        {
            return NavigationResult.NotFound(
                navigationId,
                target,
                $"No route with id '{target.RouteId}' was found.");
        }

        if (route.Kind == RouteDefinitionKind.Redirect)
        {
            if (!await CanMatchAsync(navigationId, target, route, cancellationToken))
            {
                return NavigationResult.Rejected(
                    navigationId,
                    target,
                    "CITY-NAVIGATION-MATCH-REJECTED",
                    $"Route '{route.RouteId}' was rejected by a match policy.");
            }

            return CreateStaticRedirect(navigationId, target, route, target.Parameters);
        }

        var template = RouteTemplate.Parse(routeGraph.GetFullTemplate(route));
        if (!template.TryBindParameters(target.Parameters, out var boundParameters))
        {
            return NavigationResult.Failed(
                navigationId,
                target,
                "CITY-NAVIGATION-PARAMETER-BINDING-FAILED",
                $"Parameters for route '{route.RouteId}' do not satisfy its route template.");
        }

        if (!await CanMatchAsync(navigationId, target, route, cancellationToken))
        {
            return NavigationResult.Rejected(
                navigationId,
                target,
                "CITY-NAVIGATION-MATCH-REJECTED",
                $"Route '{route.RouteId}' was rejected by a match policy.");
        }

        return await CompleteNavigationAsync(
            navigationId,
            target,
            route,
            boundParameters,
            routeGraph,
            cancellationToken);
    }

    private async ValueTask<NavigationResult> CompleteNavigationAsync(
        Guid navigationId,
        NavigationTarget target,
        RouteDescriptor route,
        IReadOnlyDictionary<string, string> parameters,
        RouteGraphSnapshot routeGraph,
        CancellationToken cancellationToken)
    {
        var targetRouteChain = GetRouteHierarchy(route, routeGraph);
        var terminalSyncRoot = new object();
        var middlewareInvocationState = new MiddlewareInvocationState();
        PreparedNavigation? prepared = null;
        var terminalInvocationCount = 0;
        var acceptingTerminalInvocations = true;
        Task<NavigationResult>? terminalTask = null;

        RouteNavigationDelegate pipeline = InvokeTerminal;

        ValueTask<NavigationResult> InvokeTerminal()
        {
            TaskCompletionSource<NavigationResult> completion;
            lock (terminalSyncRoot)
            {
                if (!acceptingTerminalInvocations)
                {
                    throw new InvalidOperationException(
                        "Routing middleware cannot invoke next after its invocation has returned.");
                }

                terminalInvocationCount++;
                if (terminalInvocationCount > 1)
                {
                    throw new InvalidOperationException("Routing middleware cannot invoke next more than once.");
                }

                completion = new TaskCompletionSource<NavigationResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                terminalTask = completion.Task;
            }

            _ = CompleteTerminalAsync(completion);
            return new ValueTask<NavigationResult>(completion.Task);
        }

        async Task CompleteTerminalAsync(TaskCompletionSource<NavigationResult> completion)
        {
            try
            {
                var result = await CompleteNavigationCoreAsync(
                    navigationId,
                    target,
                    route,
                    parameters,
                    routeGraph,
                    value => prepared = value,
                    cancellationToken);
                completion.TrySetResult(result);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        foreach (var middlewareEntry in targetRouteChain
            .SelectMany(owner => owner.MiddlewareTypes.Select(type => (Owner: owner, Type: type)))
            .Reverse())
        {
            var next = pipeline;
            pipeline = () => InvokeMiddlewareBoundaryAsync(
                navigationId,
                target,
                middlewareEntry.Owner,
                middlewareEntry.Type,
                parameters,
                next,
                middlewareInvocationState,
                cancellationToken);
        }

        NavigationResult? result = null;
        Exception? pipelineFailure = null;
        try
        {
            result = await pipeline();
        }
        catch (Exception exception)
        {
            pipelineFailure = exception;
        }

        Task<NavigationResult>? observedTerminalTask;
        lock (terminalSyncRoot)
        {
            acceptingTerminalInvocations = false;
            observedTerminalTask = terminalTask;
        }

        if (observedTerminalTask is not null)
        {
            try
            {
                await observedTerminalTask;
            }
            catch
            {
                // The middleware chain either propagated or deliberately translated the downstream failure.
            }
        }

        if (pipelineFailure is not null)
        {
            ExceptionDispatchInfo.Capture(pipelineFailure).Throw();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var completedResult = result ?? throw new InvalidOperationException(
            "Routing middleware completed without returning a navigation result.");
        if (middlewareInvocationState.HasViolation ||
            completedResult.NavigationId != navigationId ||
            !ReferenceEquals(completedResult.Target, target))
        {
            return InvalidMiddlewareResult(navigationId, target);
        }

        if (prepared is null)
        {
            return IsCompletedNavigation(completedResult)
                ? InvalidMiddlewareResult(navigationId, target)
                : completedResult;
        }

        if (terminalInvocationCount != 1 || !ReferenceEquals(completedResult, prepared.Result))
        {
            return IsCompletedNavigation(completedResult)
                ? InvalidMiddlewareResult(navigationId, target)
                : completedResult;
        }

        cancellationToken.ThrowIfCancellationRequested();
        Volatile.Write(ref _currentSnapshot, prepared.Snapshot);
        _currentRouteGraph = routeGraph;
        RecordJournalEntry(
            route,
            parameters,
            target.Options,
            prepared.ReuseKey,
            routeGraph.Version);
        return completedResult;
    }

    private async ValueTask<NavigationResult> CompleteNavigationCoreAsync(
        Guid navigationId,
        NavigationTarget target,
        RouteDescriptor route,
        IReadOnlyDictionary<string, string> parameters,
        RouteGraphSnapshot routeGraph,
        Action<PreparedNavigation> setPrepared,
        CancellationToken cancellationToken)
    {
        var currentRouteChain = CurrentSnapshot.ActiveRoute is null
            ? Array.Empty<RouteDescriptor>()
            : GetRouteHierarchy(CurrentSnapshot.ActiveRoute, _currentRouteGraph);
        var targetRouteChain = GetRouteHierarchy(route, routeGraph);
        var sharedRouteCount = target.Options.ForceReload
            ? 0
            : GetSharedRoutePrefixLength(currentRouteChain, targetRouteChain);
        var targetValidationFailure = ValidateViewModelTarget(navigationId, target, route);

        if (targetValidationFailure is not null)
        {
            return targetValidationFailure;
        }

        foreach (var leavingRoute in currentRouteChain.Skip(sharedRouteCount).Reverse())
        {
            var leaveResult = await RunLeaveGuardsAsync(
                navigationId,
                target,
                leavingRoute,
                CurrentSnapshot.Parameters,
                cancellationToken);

            if (leaveResult.Status != RouteGuardResultStatus.Allow)
            {
                return MapGuardResult(navigationId, target, leaveResult);
            }
        }

        foreach (var enteringRoute in targetRouteChain.Skip(sharedRouteCount))
        {
            var enterResult = await RunEnterGuardsAsync(
                navigationId,
                target,
                enteringRoute,
                parameters,
                cancellationToken);

            if (enterResult.Status != RouteGuardResultStatus.Allow)
            {
                return MapGuardResult(navigationId, target, enterResult);
            }
        }

        var resolution = target.Options.RestoreState && target.RestoredData is not null
            ? RouteResolutionOutcome.Success(target.RestoredData)
            : await RunResolversAsync(
                navigationId,
                target,
                targetRouteChain,
                parameters,
                cancellationToken);
        if (resolution.NavigationResult is not null)
        {
            return resolution.NavigationResult;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var reuseKey = route.ViewModelTarget?.ReuseKey;
        var result = NavigationResult.Success(navigationId, target, route, parameters);
        setPrepared(new PreparedNavigation(
            result,
            NavigationSnapshot.FromRoute(
            route,
            parameters,
            routeGraph.Version,
            reuseKey,
            resolution.Data),
            reuseKey));

        return result;
    }

    private static NavigationTarget CreateJournalTarget(NavigationJournalEntry entry)
    {
        return NavigationTarget.FromJournalEntry(
            entry.RouteId,
            entry.Parameters,
            entry.ResolvedData,
            new NavigationOptions
            {
                HistoryBehavior = NavigationHistoryBehavior.Skip,
                RestoreState = entry.CanRestoreResolvedData,
                ConcurrencyPolicy = NavigationConcurrencyPolicy.Queue,
            });
    }

    private static bool IsCompletedNavigation(NavigationResult result)
    {
        return (result.Status == NavigationResultStatus.Success ||
                result.Status == NavigationResultStatus.Redirected) &&
            result.ActiveRoute is not null;
    }

    private static bool IsStaleContributionEntry(
        NavigationJournalEntry entry,
        NavigationResult result,
        RouteGraphSnapshot graph)
    {
        return result.Status == NavigationResultStatus.NotFound &&
            !graph.TryGetRoute(entry.RouteId, out _);
    }

    private static NavigationResult JournalNotAvailable(
        Guid navigationId,
        NavigationTarget target)
    {
        return NavigationResult.Rejected(
            navigationId,
            target,
            "CITY-NAVIGATION-JOURNAL-NOT-AVAILABLE",
            "Navigation journal is not available yet.");
    }

    private void RecordJournalEntry(
        RouteDescriptor route,
        IReadOnlyDictionary<string, string> parameters,
        NavigationOptions options,
        string? reuseKey,
        long routeGraphVersion)
    {
        if (options.HistoryBehavior == NavigationHistoryBehavior.Skip)
        {
            return;
        }

        var entry = new NavigationJournalEntry(
            route.RouteId,
            parameters,
            routeGraphVersion,
            route.ContributionId,
            reuseKey,
            CurrentSnapshot.ResolvedData);

        if (options.Mode == NavigationMode.Reset)
        {
            _backStack.Clear();
            _forwardStack.Clear();
            _currentJournalEntry = entry;
            return;
        }

        if (options.Mode == NavigationMode.Replace ||
            options.HistoryBehavior == NavigationHistoryBehavior.ReplaceCurrent ||
            _currentJournalEntry is null)
        {
            _currentJournalEntry = entry;
            _forwardStack.Clear();
            TrimBackStack(options.JournalCapacity);
            return;
        }

        _backStack.Add(_currentJournalEntry);
        _currentJournalEntry = entry;
        _forwardStack.Clear();
        TrimBackStack(options.JournalCapacity);
    }

    private static NavigationJournalEntry CreateJournalEntry(NavigationSnapshot snapshot)
    {
        return new NavigationJournalEntry(
            snapshot.Route.RouteId,
            snapshot.Parameters,
            snapshot.RouteGraphVersion,
            snapshot.Route.ContributionId,
            snapshot.ReuseKey,
            snapshot.ResolvedData);
    }

    private void TrimBackStack(int capacity)
    {
        var maxBackEntries = Math.Max(0, capacity - 1);

        while (_backStack.Count > maxBackEntries)
        {
            _backStack.RemoveAt(0);
        }
    }

    private static NavigationResult? ValidateViewModelTarget(
        Guid navigationId,
        NavigationTarget target,
        RouteDescriptor route)
    {
        if (route.ViewModelTarget is null)
        {
            return NavigationResult.Failed(
                navigationId,
                target,
                "CITY-NAVIGATION-TARGET-MISSING",
                $"Route '{route.RouteId}' does not declare a ViewModel target.");
        }

        var viewModelType = route.ViewModelTarget.ViewModelType;
        if (!viewModelType.IsClass ||
            viewModelType.IsAbstract ||
            viewModelType.ContainsGenericParameters)
        {
            return NavigationResult.Failed(
                navigationId,
                target,
                "CITY-NAVIGATION-TARGET-NOT-CONSTRUCTABLE",
                $"Route '{route.RouteId}' declares non-constructable ViewModel target '{viewModelType.FullName}'.");
        }

        return null;
    }

    private static NavigationResult CreateStaticRedirect(
        Guid navigationId,
        NavigationTarget source,
        RouteDescriptor redirectRoute,
        IReadOnlyDictionary<string, string> parameters)
    {
        return NavigationResult.Redirected(
            navigationId,
            source,
            NavigationTarget.FromRouteReference(
                redirectRoute.RedirectTargetRouteId!,
                parameters,
                source.Options));
    }

    private static IReadOnlyDictionary<string, string> MergeParameters(
        IReadOnlyDictionary<string, string> source,
        IReadOnlyDictionary<string, string> matched)
    {
        var merged = new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
        foreach (var item in matched)
        {
            merged[item.Key] = item.Value;
        }

        return merged;
    }

    private static NavigationResult InvalidMiddlewareResult(Guid navigationId, NavigationTarget target)
    {
        return NavigationResult.Failed(
            navigationId,
            target,
            "CITY-NAVIGATION-MIDDLEWARE-INVALID-RESULT",
            "Routing middleware returned a completed navigation result without exactly one successful next invocation.");
    }

    private static NavigationResult? ValidateOptions(Guid navigationId, NavigationTarget target)
    {
        var options = target.Options;
        if (!Enum.IsDefined(options.Mode) ||
            !Enum.IsDefined(options.HistoryBehavior) ||
            !Enum.IsDefined(options.ConcurrencyPolicy))
        {
            return NavigationResult.Rejected(
                navigationId,
                target,
                "CITY-NAVIGATION-OPTIONS-INVALID",
                "Navigation options contain an unknown enum value.");
        }

        if (string.IsNullOrWhiteSpace(options.OutletName))
        {
            return NavigationResult.Rejected(
                navigationId,
                target,
                "CITY-NAVIGATION-OPTIONS-INVALID",
                "Navigation outlet name cannot be empty.");
        }

        if (options.Timeout is { } timeout &&
            timeout != Timeout.InfiniteTimeSpan &&
            timeout <= TimeSpan.Zero)
        {
            return NavigationResult.Rejected(
                navigationId,
                target,
                "CITY-NAVIGATION-OPTIONS-INVALID",
                "Navigation timeout must be positive or Timeout.InfiniteTimeSpan.");
        }

        return null;
    }

    private static void ApplyTimeout(
        CancellationTokenSource cancellation,
        TimeSpan? timeout)
    {
        if (!timeout.HasValue || timeout.Value == Timeout.InfiniteTimeSpan)
        {
            return;
        }

        if (timeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Navigation timeout must be positive or infinite.");
        }

        cancellation.CancelAfter(timeout.Value);
    }

    private static RouteDescriptor[] GetRouteHierarchy(
        RouteDescriptor route,
        RouteGraphSnapshot routeGraph)
    {
        var routes = new Stack<RouteDescriptor>();

        for (var current = route; current is not null; current = current.ParentRouteId is null ? null : routeGraph.GetRequiredRoute(current.ParentRouteId))
        {
            routes.Push(current);
        }

        return routes.ToArray();
    }

    private static int GetSharedRoutePrefixLength(
        IReadOnlyList<RouteDescriptor> currentRouteChain,
        IReadOnlyList<RouteDescriptor> targetRouteChain)
    {
        var sharedRouteCount = 0;

        while (sharedRouteCount < currentRouteChain.Count &&
            sharedRouteCount < targetRouteChain.Count &&
            string.Equals(
                currentRouteChain[sharedRouteCount].RouteId,
                targetRouteChain[sharedRouteCount].RouteId,
                StringComparison.Ordinal))
        {
            sharedRouteCount++;
        }

        return sharedRouteCount;
    }

    private async ValueTask<NavigationResult?> TryEnterNavigationAsync(
        Guid navigationId,
        NavigationTarget target,
        CancellationToken cancellationToken)
    {
        switch (target.Options.ConcurrencyPolicy)
        {
            case NavigationConcurrencyPolicy.RejectIfBusy:
                lock (_lifecycleGate)
                {
                    if (_disposed)
                    {
                        return ScopeDisposed(navigationId, target);
                    }

                    if (!_gate.Wait(0))
                    {
                        return NavigationResult.Rejected(
                            navigationId,
                            target,
                            "CITY-NAVIGATION-BUSY",
                            "Navigation scope is already running a navigation transaction.");
                    }
                }

                return null;
            case NavigationConcurrencyPolicy.CancelPrevious:
                return await WaitForGateAsync(navigationId, target, cancellationToken);
            case NavigationConcurrencyPolicy.Queue:
                return await WaitForGateAsync(navigationId, target, cancellationToken);
            default:
                return NavigationResult.Rejected(
                    navigationId,
                    target,
                    "CITY-NAVIGATION-CONCURRENCY-POLICY-UNSUPPORTED",
                    $"Navigation concurrency policy '{target.Options.ConcurrencyPolicy}' is not supported.");
        }
    }

    private async ValueTask<NavigationResult?> WaitForGateAsync(
        Guid navigationId,
        NavigationTarget target,
        CancellationToken cancellationToken)
    {
        CancellationTokenSource linked;
        Task waitTask;
        lock (_lifecycleGate)
        {
            if (_disposed)
            {
                return ScopeDisposed(navigationId, target);
            }

            linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _scopeCancellation.Token);
            waitTask = _gate.WaitAsync(linked.Token);
        }

        using (linked)
        {
            try
            {
                await waitTask;
            }
            catch (OperationCanceledException) when (
                IsDisposed &&
                !cancellationToken.IsCancellationRequested)
            {
                return ScopeDisposed(navigationId, target);
            }
        }

        if (!IsDisposed)
        {
            return null;
        }

        _gate.Release();
        return ScopeDisposed(navigationId, target);
    }

    private static NavigationResult ScopeDisposed(Guid navigationId, NavigationTarget target)
    {
        return NavigationResult.Rejected(
            navigationId,
            target,
            "CITY-NAVIGATION-SCOPE-DISPOSED",
            "Navigation scope has been disposed.");
    }

    private bool IsDisposed
    {
        get
        {
            lock (_lifecycleGate)
            {
                return _disposed;
            }
        }
    }

    private long? PrepareCancelPrevious(NavigationConcurrencyPolicy policy)
    {
        if (policy != NavigationConcurrencyPolicy.CancelPrevious)
        {
            return null;
        }

        CancellationTokenSource? previous;
        long version;
        lock (_lifecycleGate)
        {
            version = ++_cancelPreviousVersion;
            previous = _runningNavigationCancellation;
        }

        CancelSafely(previous);
        return version;
    }

    private CancellationTokenSource BeginRunningNavigation(
        CancellationToken cancellationToken,
        long? cancelPreviousVersion)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _scopeCancellation.Token);

        lock (_lifecycleGate)
        {
            if (_disposed ||
                (cancelPreviousVersion.HasValue && cancelPreviousVersion.Value != _cancelPreviousVersion))
            {
                cancellation.Cancel();
            }

            _runningNavigationCancellation = cancellation;
        }

        return cancellation;
    }

    private void EndRunningNavigation(CancellationTokenSource cancellation)
    {
        lock (_lifecycleGate)
        {
            if (ReferenceEquals(_runningNavigationCancellation, cancellation))
            {
                _runningNavigationCancellation = null;
            }
        }

        cancellation.Dispose();
    }

    private async ValueTask<bool> CanMatchAsync(
        Guid navigationId,
        NavigationTarget target,
        RouteDescriptor route,
        CancellationToken cancellationToken)
    {
        foreach (var policyType in route.MatchPolicyTypes)
        {
            var context = new RouteMatchPolicyContext(navigationId, target, route, CurrentSnapshot);

            bool canMatch;
            try
            {
                var policy = Resolve<IRouteMatchPolicy>(route, policyType);
                canMatch = await policy.CanMatchAsync(context, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                WritePipelineComponentDiagnostic(
                    RoutingDiagnosticIds.PipelineComponentFailed,
                    navigationId,
                    route,
                    policyType,
                    "match",
                    "CITY-NAVIGATION-MATCH-POLICY-FAILED",
                    exception.Message,
                    HostDiagnosticSeverity.Error);
                MarkComponentFailureRecorded(exception, navigationId);
                throw;
            }

            if (!canMatch)
            {
                WritePipelineComponentDiagnostic(
                    RoutingDiagnosticIds.PipelineComponentRejected,
                    navigationId,
                    route,
                    policyType,
                    "match",
                    "CITY-NAVIGATION-MATCH-REJECTED",
                    "Route match policy rejected the candidate.",
                    HostDiagnosticSeverity.Info);
                return false;
            }
        }

        return true;
    }

    private async ValueTask<RouteGuardResult> RunEnterGuardsAsync(
        Guid navigationId,
        NavigationTarget target,
        RouteDescriptor route,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        foreach (var guardType in route.EnterGuardTypes)
        {
            var context = new RouteGuardContext(navigationId, target, route, CurrentSnapshot, parameters);
            RouteGuardResult result;
            try
            {
                var guard = Resolve<IRouteEnterGuard>(route, guardType);
                result = await guard.CanEnterAsync(context, cancellationToken) ??
                    throw new InvalidOperationException(
                        $"Enter guard '{guardType.FullName}' returned a null result.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                WritePipelineComponentDiagnostic(
                    RoutingDiagnosticIds.PipelineComponentFailed,
                    navigationId,
                    route,
                    guardType,
                    "enter-guard",
                    "CITY-NAVIGATION-GUARD-FAILED",
                    exception.Message,
                    HostDiagnosticSeverity.Error);
                MarkComponentFailureRecorded(exception, navigationId);
                throw;
            }

            if (result.Status != RouteGuardResultStatus.Allow)
            {
                if (result.Status == RouteGuardResultStatus.Reject)
                {
                    WritePipelineComponentDiagnostic(
                        RoutingDiagnosticIds.PipelineComponentRejected,
                        navigationId,
                        route,
                        guardType,
                        "enter-guard",
                        result.Code,
                        result.Message,
                        HostDiagnosticSeverity.Info);
                }

                return result;
            }
        }

        return RouteGuardResult.Allow();
    }

    private async ValueTask<RouteGuardResult> RunLeaveGuardsAsync(
        Guid navigationId,
        NavigationTarget target,
        RouteDescriptor route,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        foreach (var guardType in route.LeaveGuardTypes)
        {
            var context = new RouteGuardContext(navigationId, target, route, CurrentSnapshot, parameters);
            RouteGuardResult result;
            try
            {
                var guard = Resolve<IRouteLeaveGuard>(route, guardType);
                result = await guard.CanLeaveAsync(context, cancellationToken) ??
                    throw new InvalidOperationException(
                        $"Leave guard '{guardType.FullName}' returned a null result.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                WritePipelineComponentDiagnostic(
                    RoutingDiagnosticIds.PipelineComponentFailed,
                    navigationId,
                    route,
                    guardType,
                    "leave-guard",
                    "CITY-NAVIGATION-GUARD-FAILED",
                    exception.Message,
                    HostDiagnosticSeverity.Error);
                MarkComponentFailureRecorded(exception, navigationId);
                throw;
            }

            if (result.Status != RouteGuardResultStatus.Allow)
            {
                if (result.Status == RouteGuardResultStatus.Reject)
                {
                    WritePipelineComponentDiagnostic(
                        RoutingDiagnosticIds.PipelineComponentRejected,
                        navigationId,
                        route,
                        guardType,
                        "leave-guard",
                        result.Code,
                        result.Message,
                        HostDiagnosticSeverity.Info);
                }

                return result;
            }
        }

        return RouteGuardResult.Allow();
    }

    private async ValueTask<RouteResolutionOutcome> RunResolversAsync(
        Guid navigationId,
        NavigationTarget target,
        IEnumerable<RouteDescriptor> routes,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var route in routes)
        {
            foreach (var resolverType in route.ResolverTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var context = new RouteResolveContext(
                    navigationId,
                    target,
                    route,
                    CurrentSnapshot,
                    parameters);
                RouteResolveResult result;
                try
                {
                    var resolver = Resolve<IRouteResolver>(route, resolverType);
                    result = await resolver.ResolveAsync(context, cancellationToken) ??
                        throw new InvalidOperationException(
                            $"Resolver '{resolverType.FullName}' returned a null result.");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    WritePipelineComponentDiagnostic(
                        RoutingDiagnosticIds.PipelineComponentFailed,
                        navigationId,
                        route,
                        resolverType,
                        "resolver",
                        "CITY-NAVIGATION-RESOLVER-FAILED",
                        exception.Message,
                        HostDiagnosticSeverity.Error);
                    MarkComponentFailureRecorded(exception, navigationId);
                    throw;
                }
                cancellationToken.ThrowIfCancellationRequested();

                switch (result.Status)
                {
                    case RouteResolveResultStatus.Success:
                        foreach (var item in result.Data)
                        {
                            if (!data.TryAdd(item.Key, item.Value))
                            {
                                return RouteResolutionOutcome.Failed(
                                    NavigationResult.Failed(
                                        navigationId,
                                        target,
                                        "CITY-NAVIGATION-RESOLVER-DUPLICATE-KEY",
                                        $"Resolver '{resolverType.FullName}' produced duplicate data key '{item.Key}'."));
                            }
                        }
                        break;
                    case RouteResolveResultStatus.Redirect when result.RedirectTarget is not null:
                        return RouteResolutionOutcome.Failed(
                            NavigationResult.Redirected(navigationId, target, result.RedirectTarget));
                    case RouteResolveResultStatus.NotFound:
                        return RouteResolutionOutcome.Failed(
                            NavigationResult.Failed(
                                navigationId,
                                target,
                                result.Code ?? "CITY-NAVIGATION-RESOLVER-NOT-FOUND",
                                result.Message ?? "Required route data was not found."));
                    case RouteResolveResultStatus.Cancelled:
                        return RouteResolutionOutcome.Failed(
                            NavigationResult.Cancelled(navigationId, target, result.Message));
                    case RouteResolveResultStatus.Failed:
                        WriteResolverFailed(navigationId, route, resolverType, result);
                        return RouteResolutionOutcome.Failed(
                            NavigationResult.Failed(
                                navigationId,
                                target,
                                result.Code ?? "CITY-NAVIGATION-RESOLVER-FAILED",
                                result.Message ?? "Route resolver failed.",
                                result.Exception));
                    default:
                        return RouteResolutionOutcome.Failed(
                            NavigationResult.Failed(
                                navigationId,
                                target,
                                "CITY-NAVIGATION-RESOLVER-INVALID-RESULT",
                                $"Resolver '{resolverType.FullName}' returned invalid status '{result.Status}'."));
                }
            }
        }

        return RouteResolutionOutcome.Success(data);
    }

    private TService Resolve<TService>(RouteDescriptor route, Type serviceType)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        var contributionResolver = (_routeGraphProvider as IRouteContributionServiceResolver)
            ?.GetServiceResolver(route);
        var service = contributionResolver?.Invoke(serviceType) ?? _serviceResolver(serviceType);

        if (service is TService typedService)
        {
            return typedService;
        }

        throw new InvalidOperationException(
            $"Service resolver did not return an instance of '{typeof(TService).FullName}' for '{serviceType.FullName}'.");
    }

    private static NavigationResult MapGuardResult(
        Guid navigationId,
        NavigationTarget target,
        RouteGuardResult result)
    {
        return result.Status switch
        {
            RouteGuardResultStatus.Reject => NavigationResult.Rejected(
                navigationId,
                target,
                result.Code ?? "CITY-NAVIGATION-REJECTED",
                result.Message),
            RouteGuardResultStatus.Cancel => NavigationResult.Cancelled(navigationId, target, result.Message),
            RouteGuardResultStatus.Redirect when result.RedirectTarget is not null => NavigationResult.Redirected(navigationId, target, result.RedirectTarget),
            RouteGuardResultStatus.Failed => NavigationResult.Failed(
                navigationId,
                target,
                result.Code ?? "CITY-NAVIGATION-GUARD-FAILED",
                result.Message ?? "Route guard failed.",
                result.Exception),
            _ => NavigationResult.Failed(
                navigationId,
                target,
                "CITY-NAVIGATION-GUARD-INVALID-RESULT",
                $"Route guard returned unsupported status '{result.Status}'."),
        };
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        _ = BeginDispose();
    }

    /// <summary>
    /// Executes the dispose async operation.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (IsCurrentExecutionActive())
        {
            throw new InvalidOperationException(
                "A navigation scope cannot be asynchronously disposed from its own navigation execution chain.");
        }

        await BeginDispose();
    }

    private bool IsCurrentExecutionActive()
    {
        var execution = CurrentExecution.Value;
        return execution is not null &&
            ReferenceEquals(execution.Scope, this) &&
            execution.IsActive;
    }

    private Task BeginDispose()
    {
        TaskCompletionSource? completion = null;
        Task task;
        lock (_lifecycleGate)
        {
            if (_disposeTask is not null)
            {
                return _disposeTask;
            }

            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            task = completion.Task;
            _disposeTask = task;
            _disposed = true;
        }

        CancelSafely(_scopeCancellation);
        _ = CompleteDisposeAsync(completion);
        return task;
    }

    private async Task CompleteDisposeAsync(TaskCompletionSource completion)
    {
        try
        {
            await _gate.WaitAsync();
            _gate.Dispose();
            _scopeCancellation.Dispose();
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }

    private static void CancelSafely(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (AggregateException)
        {
            // Cancellation callback failures cannot prevent lifecycle progress.
        }
        catch (ObjectDisposedException)
        {
            // A completed concurrent operation can dispose its linked source first.
        }
    }

    private void WriteNavigationStarted(
        Guid navigationId,
        NavigationTarget target,
        long graphVersion)
    {
        SafeWriteDiagnostic(new HostDiagnosticRecord(
            RoutingDiagnosticIds.NavigationStarted,
            $"Navigation '{navigationId}' started.",
            HostDiagnosticSeverity.Trace)
        {
            Context = CreateDiagnosticContext(navigationId, target, graphVersion, null, null),
        });
    }

    private void WriteNavigationCompleted(
        NavigationResult result,
        long graphVersion,
        TimeSpan elapsed)
    {
        var code = result.Status is NavigationResultStatus.Success or NavigationResultStatus.Redirected
            ? RoutingDiagnosticIds.NavigationCompleted
            : RoutingDiagnosticIds.NavigationFailed;
        SafeWriteDiagnostic(new HostDiagnosticRecord(
            code,
            $"Navigation '{result.NavigationId}' completed with status '{result.Status}'.",
            code == RoutingDiagnosticIds.NavigationCompleted ? HostDiagnosticSeverity.Info : HostDiagnosticSeverity.Warning)
        {
            Context = CreateDiagnosticContext(
                result.NavigationId,
                result.Target,
                graphVersion,
                result.Error?.Code,
                elapsed),
        });
    }

    private void WriteResolverFailed(
        Guid navigationId,
        RouteDescriptor route,
        Type resolverType,
        RouteResolveResult result)
    {
        SafeWriteDiagnostic(new HostDiagnosticRecord(
            RoutingDiagnosticIds.ResolverFailed,
            result.Message ?? "Route resolver failed.",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["operationId"] = navigationId.ToString("D"),
                ["routeId"] = route.RouteId,
                ["resolverType"] = resolverType.FullName,
                ["errorCode"] = result.Code,
            },
        });
    }

    private void SafeWriteDiagnostic(HostDiagnosticRecord record)
    {
        if (_diagnostics is null)
        {
            return;
        }

        try
        {
            _diagnostics.Write(record);
        }
        catch
        {
            // Diagnostics are observational and cannot change navigation results.
        }
    }

    private async ValueTask<NavigationResult> InvokeMiddlewareBoundaryAsync(
        Guid navigationId,
        NavigationTarget target,
        RouteDescriptor route,
        Type middlewareType,
        IReadOnlyDictionary<string, string> parameters,
        RouteNavigationDelegate next,
        MiddlewareInvocationState invocationState,
        CancellationToken cancellationToken)
    {
        var syncRoot = new object();
        var acceptingNext = true;
        var invocationCount = 0;
        Task<NavigationResult>? nextTask = null;

        ValueTask<NavigationResult> GuardedNext()
        {
            TaskCompletionSource<NavigationResult> completion;
            lock (syncRoot)
            {
                if (!acceptingNext)
                {
                    invocationState.MarkViolation();
                    throw new InvalidOperationException(
                        "Routing middleware cannot invoke next after its invocation has returned.");
                }

                invocationCount++;
                if (invocationCount > 1)
                {
                    invocationState.MarkViolation();
                    throw new InvalidOperationException("Routing middleware cannot invoke next more than once.");
                }

                completion = new TaskCompletionSource<NavigationResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                nextTask = completion.Task;
            }

            _ = CompleteNextAsync(completion);
            return new ValueTask<NavigationResult>(completion.Task);
        }

        async Task CompleteNextAsync(TaskCompletionSource<NavigationResult> completion)
        {
            try
            {
                completion.TrySetResult(await next());
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                completion.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        NavigationResult? result = null;
        Exception? middlewareFailure = null;
        try
        {
            result = await InvokeMiddlewareAsync(
                navigationId,
                target,
                route,
                middlewareType,
                parameters,
                GuardedNext,
                cancellationToken);
        }
        catch (Exception exception)
        {
            middlewareFailure = exception;
        }

        Task<NavigationResult>? observedNextTask;
        lock (syncRoot)
        {
            acceptingNext = false;
            observedNextTask = nextTask;
        }

        if (observedNextTask is not null)
        {
            try
            {
                await observedNextTask;
            }
            catch
            {
                // Middleware can deliberately translate a downstream failure.
            }
        }

        if (middlewareFailure is not null)
        {
            ExceptionDispatchInfo.Capture(middlewareFailure).Throw();
        }

        return result ?? throw new InvalidOperationException(
            $"Navigation middleware '{middlewareType.FullName}' completed without returning a result.");
    }

    private async ValueTask<NavigationResult> InvokeMiddlewareAsync(
        Guid navigationId,
        NavigationTarget target,
        RouteDescriptor route,
        Type middlewareType,
        IReadOnlyDictionary<string, string> parameters,
        RouteNavigationDelegate next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await Resolve<IRouteNavigationMiddleware>(route, middlewareType).InvokeAsync(
                new RouteNavigationMiddlewareContext(
                    navigationId,
                    target,
                    route,
                    CurrentSnapshot,
                    parameters),
                next,
                cancellationToken) ??
                throw new InvalidOperationException(
                    $"Navigation middleware '{middlewareType.FullName}' returned a null result.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (!IsComponentFailureRecorded(exception, navigationId))
            {
                WritePipelineComponentDiagnostic(
                    RoutingDiagnosticIds.PipelineComponentFailed,
                    navigationId,
                    route,
                    middlewareType,
                    "middleware",
                    "CITY-NAVIGATION-MIDDLEWARE-FAILED",
                    exception.Message,
                    HostDiagnosticSeverity.Error);
                MarkComponentFailureRecorded(exception, navigationId);
            }

            throw;
        }
    }

    private static bool IsComponentFailureRecorded(Exception exception, Guid navigationId) =>
        exception.Data[ComponentFailureRecordedKey] is Guid recordedNavigationId &&
        recordedNavigationId == navigationId;

    private static void MarkComponentFailureRecorded(Exception exception, Guid navigationId) =>
        exception.Data[ComponentFailureRecordedKey] = navigationId;

    private void WritePipelineComponentDiagnostic(
        string diagnosticCode,
        Guid navigationId,
        RouteDescriptor route,
        Type componentType,
        string stage,
        string? errorCode,
        string? message,
        HostDiagnosticSeverity severity)
    {
        SafeWriteDiagnostic(new HostDiagnosticRecord(
            diagnosticCode,
            message ?? $"Routing component '{componentType.FullName}' completed with a non-success result.",
            severity)
        {
            Context = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["operationId"] = navigationId.ToString("D"),
                ["routeId"] = route.RouteId,
                ["componentType"] = componentType.FullName,
                ["stage"] = stage,
                ["errorCode"] = errorCode,
            },
        });
    }

    private static IReadOnlyDictionary<string, string?> CreateDiagnosticContext(
        Guid navigationId,
        NavigationTarget target,
        long graphVersion,
        string? errorCode,
        TimeSpan? elapsed)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["operationId"] = navigationId.ToString("D"),
            ["target"] = target.ToString(),
            ["targetKind"] = target.Kind.ToString(),
            ["graphVersion"] = graphVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["errorCode"] = errorCode,
            ["elapsedMilliseconds"] = elapsed?.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    private sealed class RouteResolutionOutcome
    {
        private RouteResolutionOutcome(
            IReadOnlyDictionary<string, object?> data,
            NavigationResult? navigationResult)
        {
            Data = data;
            NavigationResult = navigationResult;
        }

        public IReadOnlyDictionary<string, object?> Data { get; }
        public NavigationResult? NavigationResult { get; }

        public static RouteResolutionOutcome Success(IReadOnlyDictionary<string, object?> data) =>
            new(data, null);

        public static RouteResolutionOutcome Failed(NavigationResult result) =>
            new(new Dictionary<string, object?>(), result);
    }

    private sealed class PreparedNavigation
    {
        public PreparedNavigation(
            NavigationResult result,
            NavigationSnapshot snapshot,
            string? reuseKey)
        {
            Result = result;
            Snapshot = snapshot;
            ReuseKey = reuseKey;
        }

        public NavigationResult Result { get; }

        public NavigationSnapshot Snapshot { get; }

        public string? ReuseKey { get; }
    }

    private sealed class NavigationExecution
    {
        private int _active = 1;

        public NavigationExecution(NavigationScope scope)
        {
            Scope = scope;
        }

        public NavigationScope Scope { get; }

        public bool IsActive => Volatile.Read(ref _active) == 1;

        public void Deactivate() => Interlocked.Exchange(ref _active, 0);
    }

    private sealed class MiddlewareInvocationState
    {
        private int _hasViolation;

        public bool HasViolation => Volatile.Read(ref _hasViolation) == 1;

        public void MarkViolation() => Interlocked.Exchange(ref _hasViolation, 1);
    }

    private sealed class NavigationTargetIdentity : IEquatable<NavigationTargetIdentity>
    {
        private readonly KeyValuePair<string, string>[] _parameters;

        public NavigationTargetIdentity(NavigationTarget target)
        {
            Kind = target.Kind;
            OutletName = target.Options.OutletName;
            RouteId = target.RouteId;
            Path = target.Path;
            _parameters = target.Parameters
                .OrderBy(parameter => parameter.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private NavigationTargetKind Kind { get; }
        private string OutletName { get; }
        private string? RouteId { get; }
        private string? Path { get; }

        public bool Equals(NavigationTargetIdentity? other)
        {
            if (other is null ||
                Kind != other.Kind ||
                !string.Equals(OutletName, other.OutletName, StringComparison.Ordinal) ||
                !string.Equals(RouteId, other.RouteId, StringComparison.Ordinal) ||
                !string.Equals(Path, other.Path, StringComparison.Ordinal) ||
                _parameters.Length != other._parameters.Length)
            {
                return false;
            }

            for (var index = 0; index < _parameters.Length; index++)
            {
                if (!string.Equals(
                        _parameters[index].Key,
                        other._parameters[index].Key,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        _parameters[index].Value,
                        other._parameters[index].Value,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as NavigationTargetIdentity);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Kind);
            hash.Add(OutletName, StringComparer.Ordinal);
            hash.Add(RouteId, StringComparer.Ordinal);
            hash.Add(Path, StringComparer.Ordinal);
            foreach (var parameter in _parameters)
            {
                hash.Add(parameter.Key, StringComparer.OrdinalIgnoreCase);
                hash.Add(parameter.Value, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }

    private sealed class NavigationJournalEntry
    {
        public NavigationJournalEntry(
            string routeId,
            IReadOnlyDictionary<string, string> parameters,
            long routeGraphVersion,
            string? contributionId,
            string? reuseKey,
            IReadOnlyDictionary<string, object?> resolvedData)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(routeId);
            ArgumentNullException.ThrowIfNull(parameters);

            RouteId = routeId;
            Parameters = RouteParameters.Copy(parameters);
            RouteGraphVersion = routeGraphVersion;
            ContributionId = string.IsNullOrWhiteSpace(contributionId) ? null : contributionId;
            ReuseKey = string.IsNullOrWhiteSpace(reuseKey) ? null : reuseKey;
            CanRestoreResolvedData = resolvedData.All(item => IsJournalSafeValue(item.Value));
            ResolvedData = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
                resolvedData
                    .Where(item => IsJournalSafeValue(item.Value))
                    .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal));
        }

        public string RouteId { get; }

        public IReadOnlyDictionary<string, string> Parameters { get; }

        public long RouteGraphVersion { get; }

        public string? ContributionId { get; }

        public string? ReuseKey { get; }

        public bool CanRestoreResolvedData { get; }

        public IReadOnlyDictionary<string, object?> ResolvedData { get; }

        private static bool IsJournalSafeValue(object? value)
        {
            return value is null or string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or
                float or double or decimal or char or Guid or DateTime or DateTimeOffset or TimeSpan;
        }
    }

    private sealed class FixedRouteGraphProvider : IRouteGraphProvider
    {
        public FixedRouteGraphProvider(RouteGraphSnapshot snapshot)
        {
            CurrentSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public RouteGraphSnapshot CurrentSnapshot { get; }
    }
}
