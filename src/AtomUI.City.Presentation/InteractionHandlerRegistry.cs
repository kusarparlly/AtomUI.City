using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Mvvm;
using AtomUI.City.Core.Threading;

namespace AtomUI.City.Presentation;

public sealed class InteractionHandlerRegistry : IInteractionHandlerRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<InteractionHandlerKey, List<HandlerRegistration>> _registrations = [];
    private readonly Dictionary<string, BoundedSerialExecutionLane> _modalLanes = new(StringComparer.Ordinal);
    private readonly IUiDispatcher _dispatcher;
    private readonly IHostDiagnostics? _diagnostics;
    private readonly PresentationQueueOptions _queueOptions;

    public InteractionHandlerRegistry(IUiDispatcher dispatcher)
        : this(dispatcher, diagnostics: null, queueOptions: null)
    {
    }

    public InteractionHandlerRegistry(
        IUiDispatcher dispatcher,
        IHostDiagnostics? diagnostics)
        : this(dispatcher, diagnostics, queueOptions: null)
    {
    }

    public InteractionHandlerRegistry(
        IUiDispatcher dispatcher,
        IHostDiagnostics? diagnostics,
        PresentationQueueOptions? queueOptions)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        queueOptions ??= new PresentationQueueOptions();
        queueOptions.Validate();

        _dispatcher = dispatcher;
        _diagnostics = diagnostics;
        _queueOptions = queueOptions;
    }

    public IDisposable Register<TRequest, TResult>(
        Func<InteractionContext<TRequest>, CancellationToken, ValueTask<TResult>> handler,
        IActivationScope? activationScope = null)
    {
        return Register(
            handler,
            new InteractionHandlerRegistrationOptions
            {
                ActivationScope = activationScope,
            });
    }

    public IDisposable Register<TRequest, TResult>(
        Func<InteractionContext<TRequest>, CancellationToken, ValueTask<TResult>> handler,
        InteractionHandlerRegistrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        var key = InteractionHandlerKey.Create<TRequest, TResult>();
        var registration = new HandlerRegistration<TRequest, TResult>(
            this,
            key,
            handler,
            options);

        lock (_gate)
        {
            if (!_registrations.TryGetValue(key, out var registrations))
            {
                registrations = [];
                _registrations[key] = registrations;
            }

            registrations.Add(registration);
        }

        options.ActivationScope?.Add(registration);

        return registration;
    }

    public async ValueTask<InteractionResult<TResult>> HandleAsync<TRequest, TResult>(
        TRequest request,
        CancellationToken cancellationToken = default)
    {
        return await HandleAsync<TRequest, TResult>(
            request,
            InteractionDispatchContext.Global,
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<InteractionResult<TResult>> HandleAsync<TRequest, TResult>(
        TRequest request,
        InteractionDispatchContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var registration = FindRegistration<TRequest, TResult>(context);
        if (registration is null)
        {
            WriteNotHandledDiagnostic<TRequest, TResult>();

            return InteractionResult<TResult>.NotHandled();
        }

        if (!context.IsModal)
        {
            return await ExecuteInteractionAsync<TRequest, TResult>(
                request,
                context,
                registration,
                cancellationToken).ConfigureAwait(false);
        }

        var lane = GetModalLane(context.WindowId);
        var completion = new TaskCompletionSource<InteractionResult<TResult>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!lane.TrySchedule(async () =>
            {
                try
                {
                    completion.TrySetResult(await ExecuteInteractionAsync<TRequest, TResult>(
                        request,
                        context,
                        registration,
                        cancellationToken).ConfigureAwait(false));
                }
                catch (Exception exception)
                {
                    completion.TrySetException(exception);
                }
            }))
        {
            var exception = new PresentationException(
                PresentationError.InteractionQueueFull,
                $"The modal interaction queue for window '{NormalizeWindowId(context.WindowId)}' is full.");
            WriteQueueRejectedDiagnostic<TRequest, TResult>(context, lane.Snapshot, exception);
            return InteractionResult<TResult>.Failed(exception);
        }

        try
        {
            return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return InteractionResult<TResult>.Canceled();
        }
    }

    public PresentationQueueSnapshot GetModalQueueSnapshot(string? windowId = null)
    {
        var laneId = NormalizeWindowId(windowId);
        lock (_gate)
        {
            return _modalLanes.TryGetValue(laneId, out var lane)
                ? lane.Snapshot
                : new PresentationQueueSnapshot(
                    _queueOptions.ModalInteractionPendingCapacity,
                    PendingCount: 0,
                    InFlightCount: 0,
                    PeakPendingCount: 0,
                    RejectedCount: 0);
        }
    }

    public int RevokePlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        return Revoke(registration => string.Equals(registration.PluginId, pluginId, StringComparison.Ordinal));
    }

    public int RevokeContribution(string contributionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);

        return Revoke(
            registration => string.Equals(registration.ContributionId, contributionId, StringComparison.Ordinal));
    }

    private HandlerRegistration<TRequest, TResult>? FindRegistration<TRequest, TResult>(
        InteractionDispatchContext context)
    {
        var key = InteractionHandlerKey.Create<TRequest, TResult>();

        lock (_gate)
        {
            if (!_registrations.TryGetValue(key, out var registrations))
            {
                return null;
            }

            return registrations
                .OfType<HandlerRegistration<TRequest, TResult>>()
                .Where(registration => !registration.IsDisposed && registration.Matches(context))
                .OrderBy(static registration => registration.Scope)
                .LastOrDefault();
        }
    }

    private static void ValidateOptions(InteractionHandlerRegistrationOptions options)
    {
        var scope = options.Scope;

        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Interaction handler scope must be defined.");
        }

        if (scope == InteractionHandlerScope.Window && string.IsNullOrWhiteSpace(options.WindowId))
        {
            throw new ArgumentException("A window-scoped interaction handler requires WindowId.", nameof(options));
        }

        if (scope == InteractionHandlerScope.Route && string.IsNullOrWhiteSpace(options.RouteId))
        {
            throw new ArgumentException("A route-scoped interaction handler requires RouteId.", nameof(options));
        }

        if (scope == InteractionHandlerScope.Activation && options.ActivationScope is null)
        {
            throw new ArgumentException("An activation-scoped interaction handler requires ActivationScope.", nameof(options));
        }
    }

    private async ValueTask<InteractionResult<TResult>> ExecuteInteractionAsync<TRequest, TResult>(
        TRequest request,
        InteractionDispatchContext dispatchContext,
        HandlerRegistration<TRequest, TResult> registration,
        CancellationToken cancellationToken)
    {
        var tokens = new List<CancellationToken>
        {
            cancellationToken,
            registration.CancellationToken,
        };
        if (registration.ActivationScope is not null)
        {
            tokens.Add(registration.ActivationScope.CancellationToken);
        }
        if (dispatchContext.ActivationScope is not null)
        {
            tokens.Add(dispatchContext.ActivationScope.CancellationToken);
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(tokens.ToArray());
        try
        {
            TResult? value = default;
            await _dispatcher.PostAsync(
                async dispatcherCancellationToken =>
                {
                    using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                        linkedCancellation.Token,
                        dispatcherCancellationToken);
                    var context = new InteractionContext<TRequest>(request);
                    value = await registration
                        .HandleAsync(context, executionCancellation.Token)
                        .ConfigureAwait(false);
                },
                linkedCancellation.Token).ConfigureAwait(false);

            WriteHandledDiagnostic<TRequest, TResult>(registration);
            return InteractionResult<TResult>.Completed(value!);
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            return InteractionResult<TResult>.Canceled();
        }
        catch (Exception exception)
        {
            WriteFailedDiagnostic<TRequest, TResult>(registration, exception);
            return InteractionResult<TResult>.Failed(exception);
        }
    }

    private BoundedSerialExecutionLane GetModalLane(string? windowId)
    {
        var laneId = NormalizeWindowId(windowId);
        lock (_gate)
        {
            if (_modalLanes.TryGetValue(laneId, out var existing))
            {
                return existing;
            }

            BoundedSerialExecutionLane? created = null;
            created = new BoundedSerialExecutionLane(
                _queueOptions.ModalInteractionPendingCapacity,
                () => RemoveIdleModalLane(laneId, created));
            _modalLanes.Add(laneId, created);
            return created;
        }
    }

    private void RemoveIdleModalLane(string laneId, BoundedSerialExecutionLane? lane)
    {
        if (lane is null)
        {
            return;
        }

        lock (_gate)
        {
            if (_modalLanes.TryGetValue(laneId, out var current) &&
                ReferenceEquals(current, lane) &&
                lane.IsIdle)
            {
                _modalLanes.Remove(laneId);
            }
        }
    }

    private int Revoke(Func<HandlerRegistration, bool> predicate)
    {
        List<HandlerRegistration> registrations;

        lock (_gate)
        {
            registrations = _registrations
                .Values
                .SelectMany(static items => items)
                .Where(registration => !registration.IsDisposed && predicate(registration))
                .ToList();
        }

        foreach (var registration in registrations)
        {
            registration.Dispose();
            WriteRevokedDiagnostic(registration);
        }

        return registrations.Count;
    }

    private void Remove(HandlerRegistration registration)
    {
        lock (_gate)
        {
            if (!_registrations.TryGetValue(registration.Key, out var registrations))
            {
                return;
            }

            registrations.Remove(registration);
            if (registrations.Count == 0)
            {
                _registrations.Remove(registration.Key);
            }
        }
    }

    private void WriteHandledDiagnostic<TRequest, TResult>(HandlerRegistration registration)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.InteractionHandled,
            $"Presentation interaction handler completed request '{typeof(TRequest).FullName}' with result '{typeof(TResult).FullName}'.",
            HostDiagnosticSeverity.Info)
        {
            Context = CreateDiagnosticContext<TRequest, TResult>(
                InteractionResultStatus.Completed,
                registration,
                exception: null),
        });
    }

    private void WriteNotHandledDiagnostic<TRequest, TResult>()
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.InteractionNotHandled,
            $"Presentation interaction handler was not found for request '{typeof(TRequest).FullName}' with result '{typeof(TResult).FullName}'.",
            HostDiagnosticSeverity.Warning)
        {
            Context = CreateDiagnosticContext<TRequest, TResult>(
                InteractionResultStatus.NotHandled,
                registration: null,
                exception: null),
        });
    }

    private void WriteFailedDiagnostic<TRequest, TResult>(
        HandlerRegistration registration,
        Exception exception)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.InteractionFailed,
            $"Presentation interaction handler failed for request '{typeof(TRequest).FullName}' with result '{typeof(TResult).FullName}': {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = CreateDiagnosticContext<TRequest, TResult>(
                InteractionResultStatus.Failed,
                registration,
                exception),
        });
    }

    private void WriteQueueRejectedDiagnostic<TRequest, TResult>(
        InteractionDispatchContext context,
        PresentationQueueSnapshot snapshot,
        Exception exception)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.InteractionQueueRejected,
            $"Presentation rejected modal interaction '{typeof(TRequest).FullName}' because window '{NormalizeWindowId(context.WindowId)}' reached its queue capacity.",
            HostDiagnosticSeverity.Warning)
        {
            Context = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["requestType"] = typeof(TRequest).FullName,
                ["resultType"] = typeof(TResult).FullName,
                ["windowId"] = NormalizeWindowId(context.WindowId),
                ["capacity"] = snapshot.Capacity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["pendingCount"] = snapshot.PendingCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["rejectedCount"] = snapshot.RejectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["error"] = exception.GetType().FullName,
            },
        });
    }

    private void WriteRevokedDiagnostic(HandlerRegistration registration)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.InteractionHandlerRevoked,
            $"Presentation interaction handler revoked plugin '{Normalize(registration.PluginId)}' contribution '{Normalize(registration.ContributionId)}'.",
            HostDiagnosticSeverity.Info)
        {
            Context = CreateDiagnosticContext(
                registration.Key.RequestType,
                registration.Key.ResultType,
                status: "Revoked",
                registration,
                exception: null),
        });
    }

    private static IReadOnlyDictionary<string, string?> CreateDiagnosticContext<TRequest, TResult>(
        InteractionResultStatus status,
        HandlerRegistration? registration,
        Exception? exception)
    {
        return CreateDiagnosticContext(
            typeof(TRequest),
            typeof(TResult),
            status.ToString(),
            registration,
            exception);
    }

    private static IReadOnlyDictionary<string, string?> CreateDiagnosticContext(
        Type requestType,
        Type resultType,
        string status,
        HandlerRegistration? registration,
        Exception? exception)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["requestType"] = requestType.FullName,
            ["resultType"] = resultType.FullName,
            ["status"] = status,
            ["pluginId"] = registration?.PluginId,
            ["contributionId"] = registration?.ContributionId,
            ["error"] = exception?.GetType().FullName,
        };
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<none>" : value;
    }

    private static string NormalizeWindowId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<presentation>" : value;
    }

    private abstract class HandlerRegistration : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly CancellationToken _cancellationToken;
        private readonly InteractionHandlerRegistry _registry;
        private int _disposed;

        protected HandlerRegistration(
            InteractionHandlerRegistry registry,
            InteractionHandlerKey key,
            InteractionHandlerRegistrationOptions options)
        {
            _registry = registry;
            _cancellationToken = _cancellation.Token;
            Key = key;
            ActivationScope = options.ActivationScope;
            Scope = options.Scope;
            WindowId = NormalizeOptional(options.WindowId);
            RouteId = NormalizeOptional(options.RouteId);
            PluginId = string.IsNullOrWhiteSpace(options.PluginId) ? null : options.PluginId;
            ContributionId = string.IsNullOrWhiteSpace(options.ContributionId) ? null : options.ContributionId;
        }

        public InteractionHandlerKey Key { get; }

        public IActivationScope? ActivationScope { get; }

        public InteractionHandlerScope Scope { get; }

        public string? WindowId { get; }

        public string? RouteId { get; }

        public CancellationToken CancellationToken => _cancellationToken;

        public string? PluginId { get; }

        public string? ContributionId { get; }

        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public bool Matches(InteractionDispatchContext context)
        {
            return Scope switch
            {
                InteractionHandlerScope.Presentation => true,
                InteractionHandlerScope.Window => string.Equals(WindowId, context.WindowId, StringComparison.Ordinal),
                InteractionHandlerScope.Route =>
                    string.Equals(RouteId, context.RouteId, StringComparison.Ordinal) &&
                    (WindowId is null || string.Equals(WindowId, context.WindowId, StringComparison.Ordinal)),
                InteractionHandlerScope.Activation => ReferenceEquals(ActivationScope, context.ActivationScope),
                _ => false,
            };
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _cancellation.Cancel();
            _registry.Remove(this);
            _cancellation.Dispose();
        }

        private static string? NormalizeOptional(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed class HandlerRegistration<TRequest, TResult> : HandlerRegistration
    {
        private readonly Func<InteractionContext<TRequest>, CancellationToken, ValueTask<TResult>> _handler;

        public HandlerRegistration(
            InteractionHandlerRegistry registry,
            InteractionHandlerKey key,
            Func<InteractionContext<TRequest>, CancellationToken, ValueTask<TResult>> handler,
            InteractionHandlerRegistrationOptions options)
            : base(registry, key, options)
        {
            _handler = handler;
        }

        public ValueTask<TResult> HandleAsync(
            InteractionContext<TRequest> context,
            CancellationToken cancellationToken)
        {
            return _handler(context, cancellationToken);
        }
    }

    private readonly record struct InteractionHandlerKey(
        Type RequestType,
        Type ResultType)
    {
        public static InteractionHandlerKey Create<TRequest, TResult>()
        {
            return new InteractionHandlerKey(typeof(TRequest), typeof(TResult));
        }
    }
}
