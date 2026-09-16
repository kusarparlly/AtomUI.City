using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security;

/// <summary>
/// Represents command authorization source.
/// </summary>
public sealed class CommandAuthorizationSource : ICommandAuthorizationSource, IDisposable
{
    private readonly IAuthorizationEvaluator _authorizationEvaluator;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICommandAuthorizationDescriptorProvider _descriptorProvider;
    private readonly IAuthenticationStateProvider? _authenticationStateProvider;
    private readonly IPermissionRegistry? _permissionRegistry;
    private readonly IHostDiagnostics? _diagnostics;
    private readonly OrderedEventPublisher<CommandAuthorizationChangedEventArgs> _eventPublisher;
    private readonly object _syncRoot = new();
    private long _revision;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <c>CommandAuthorizationSource</c> type.
    /// </summary>
    public CommandAuthorizationSource(
        IAuthorizationEvaluator authorizationEvaluator,
        ICurrentPrincipalAccessor principalAccessor,
        ICommandAuthorizationDescriptorProvider descriptorProvider,
        IAuthenticationStateProvider? authenticationStateProvider = null,
        IPermissionRegistry? permissionRegistry = null)
        : this(
            authorizationEvaluator,
            principalAccessor,
            descriptorProvider,
            authenticationStateProvider,
            permissionRegistry,
            diagnostics: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>CommandAuthorizationSource</c> type.
    /// </summary>
    public CommandAuthorizationSource(
        IAuthorizationEvaluator authorizationEvaluator,
        ICurrentPrincipalAccessor principalAccessor,
        ICommandAuthorizationDescriptorProvider descriptorProvider,
        IAuthenticationStateProvider? authenticationStateProvider,
        IPermissionRegistry? permissionRegistry,
        IHostDiagnostics? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(authorizationEvaluator);
        ArgumentNullException.ThrowIfNull(principalAccessor);
        ArgumentNullException.ThrowIfNull(descriptorProvider);

        _authorizationEvaluator = authorizationEvaluator;
        _principalAccessor = principalAccessor;
        _descriptorProvider = descriptorProvider;
        _authenticationStateProvider = authenticationStateProvider ?? principalAccessor as IAuthenticationStateProvider;
        _permissionRegistry = permissionRegistry;
        _diagnostics = diagnostics;
        _eventPublisher = new OrderedEventPublisher<CommandAuthorizationChangedEventArgs>(
            diagnostics,
            SecurityDiagnosticIds.CommandAuthorizationObserverFailed);

        var authenticationSubscriptionAttempted = false;
        var descriptorSubscriptionAttempted = false;
        var permissionSubscriptionAttempted = false;

        try
        {
            if (_authenticationStateProvider is not null)
            {
                authenticationSubscriptionAttempted = true;
                _authenticationStateProvider.StateChanged += OnAuthenticationStateChanged;
            }

            descriptorSubscriptionAttempted = true;
            _descriptorProvider.DescriptorChanged += OnDescriptorChanged;

            if (_permissionRegistry is not null)
            {
                permissionSubscriptionAttempted = true;
                _permissionRegistry.Changed += OnPermissionRegistryChanged;
            }
        }
        catch (Exception exception)
        {
            lock (_syncRoot)
            {
                _disposed = true;
            }

            var rollbackFailures = new List<Exception>();
            if (permissionSubscriptionAttempted)
            {
                CaptureFailure(
                    () => _permissionRegistry!.Changed -= OnPermissionRegistryChanged,
                    rollbackFailures);
            }

            if (descriptorSubscriptionAttempted)
            {
                CaptureFailure(
                    () => _descriptorProvider.DescriptorChanged -= OnDescriptorChanged,
                    rollbackFailures);
            }

            if (authenticationSubscriptionAttempted)
            {
                CaptureFailure(
                    () => _authenticationStateProvider!.StateChanged -= OnAuthenticationStateChanged,
                    rollbackFailures);
            }

            _eventPublisher.Complete();
            WriteLifecycleFailure("Subscribe", exception);

            foreach (var rollbackFailure in rollbackFailures)
            {
                WriteLifecycleFailure("SubscribeRollback", rollbackFailure);
            }

            if (rollbackFailures.Count > 0)
            {
                rollbackFailures.Insert(0, exception);
                throw new AggregateException(
                    "Command authorization source subscription and rollback failed.",
                    rollbackFailures);
            }

            throw;
        }
    }

    /// <summary>
    /// Occurs when authorization changed.
    /// </summary>
    public event EventHandler<CommandAuthorizationChangedEventArgs>? AuthorizationChanged;

    /// <summary>
    /// Executes the get state async operation.
    /// </summary>
    public async ValueTask<CommandAuthorizationState> GetStateAsync(
        CommandAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (cancellationToken.IsCancellationRequested)
        {
            return Complete(context, descriptor: null, CreateCancelledState(context));
        }

        CommandAuthorizationDescriptor? descriptor = null;

        try
        {
            descriptor = await _descriptorProvider.GetDescriptorAsync(context, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return Complete(context, descriptor, CreateCancelledState(context));
            }

            if (descriptor is null)
            {
                return Complete(context, descriptor, new CommandAuthorizationState(
                    context.CommandId,
                    canExecute: true,
                    isVisible: true,
                    CommandUnauthorizedBehavior.Disable,
                    AuthorizationResult.Allowed(),
                    Interlocked.Read(ref _revision)));
            }

            var result = await EvaluateDescriptorAsync(context, descriptor, cancellationToken)
                .ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return Complete(context, descriptor, CreateCancelledState(context));
            }

            var canExecute = result.Succeeded;
            var isVisible = result.Succeeded || descriptor.UnauthorizedBehavior != CommandUnauthorizedBehavior.Hide;

            return Complete(context, descriptor, new CommandAuthorizationState(
                context.CommandId,
                canExecute,
                isVisible,
                descriptor.UnauthorizedBehavior,
                result,
                Interlocked.Read(ref _revision),
                descriptor.DeniedMessageKey));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Complete(context, descriptor, CreateCancelledState(context));
        }
        catch (Exception exception)
        {
            return Complete(context, descriptor, CreateFailedState(context, descriptor, exception));
        }
    }

    /// <summary>
    /// Executes the check execution async operation.
    /// </summary>
    public async ValueTask<AuthorizationResult> CheckExecutionAsync(
        CommandAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (cancellationToken.IsCancellationRequested)
        {
            return Complete(context, descriptor: null, AuthorizationResult.Cancelled());
        }

        try
        {
            var descriptor = await _descriptorProvider.GetDescriptorAsync(context, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return Complete(context, descriptor, AuthorizationResult.Cancelled());
            }

            var result = descriptor is null
                ? AuthorizationResult.Allowed()
                : await EvaluateDescriptorAsync(context, descriptor, cancellationToken)
                    .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return Complete(context, descriptor, AuthorizationResult.Cancelled());
            }

            return Complete(context, descriptor, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Complete(context, descriptor: null, AuthorizationResult.Cancelled());
        }
        catch (Exception exception)
        {
            return Complete(context, descriptor: null, CreateFailedResult(context, exception));
        }
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        var failures = new List<Exception>();
        if (_authenticationStateProvider is not null)
        {
            CaptureFailure(
                () => _authenticationStateProvider.StateChanged -= OnAuthenticationStateChanged,
                failures);
        }

        CaptureFailure(
            () => _descriptorProvider.DescriptorChanged -= OnDescriptorChanged,
            failures);

        if (_permissionRegistry is not null)
        {
            CaptureFailure(
                () => _permissionRegistry.Changed -= OnPermissionRegistryChanged,
                failures);
        }

        _eventPublisher.Complete();

        foreach (var failure in failures)
        {
            WriteLifecycleFailure("Dispose", failure);
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(
                "One or more command authorization subscriptions could not be released.",
                failures);
        }
    }

    private ValueTask<AuthorizationResult> EvaluateDescriptorAsync(
        CommandAuthorizationContext context,
        CommandAuthorizationDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        return _authorizationEvaluator.EvaluateAsync(
            new AuthorizationRequest(
                _principalAccessor.Principal,
                descriptor.Policy,
                context.ResourceName ?? context.CommandId,
                context.ContributionId ?? descriptor.ContributionId),
            cancellationToken);
    }

    private CommandAuthorizationState CreateCancelledState(CommandAuthorizationContext context)
    {
        return new CommandAuthorizationState(
            context.CommandId,
            canExecute: false,
            isVisible: true,
            CommandUnauthorizedBehavior.Disable,
            AuthorizationResult.Cancelled(),
            Interlocked.Read(ref _revision));
    }

    private CommandAuthorizationState CreateFailedState(
        CommandAuthorizationContext context,
        CommandAuthorizationDescriptor? descriptor,
        Exception exception)
    {
        var unauthorizedBehavior = descriptor?.UnauthorizedBehavior ?? CommandUnauthorizedBehavior.Disable;
        return new CommandAuthorizationState(
            context.CommandId,
            canExecute: false,
            isVisible: unauthorizedBehavior != CommandUnauthorizedBehavior.Hide,
            unauthorizedBehavior,
            CreateFailedResult(context, exception),
            Interlocked.Read(ref _revision),
            descriptor?.DeniedMessageKey);
    }

    private AuthorizationResult CreateFailedResult(
        CommandAuthorizationContext context,
        Exception exception)
    {
        var result = AuthorizationResult.Failed(
            SecurityFailureKind.EvaluatorFailed,
            context.CommandId,
            exception.Message,
            exception: exception);
        SecurityDiagnostics.Write(
            _diagnostics,
            SecurityDiagnosticIds.CommandAuthorizationFailed,
            "Command authorization failed.",
            HostDiagnosticSeverity.Error,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["commandId"] = context.CommandId,
                ["resourceName"] = context.ResourceName,
                ["contributionId"] = context.ContributionId,
                ["exceptionType"] = exception.GetType().FullName,
            });
        return result;
    }

    private void OnAuthenticationStateChanged(
        object? sender,
        AuthenticationStateChangedEventArgs args)
    {
        PublishChanged(CommandAuthorizationChangeReason.AuthenticationStateChanged);
    }

    private void OnDescriptorChanged(
        object? sender,
        CommandAuthorizationChangedEventArgs args)
    {
        PublishChanged(CommandAuthorizationChangeReason.DescriptorChanged, args.CommandId);
    }

    private void OnPermissionRegistryChanged(
        object? sender,
        PermissionRegistryChangedEventArgs args)
    {
        PublishChanged(CommandAuthorizationChangeReason.PermissionChanged);
    }

    private void PublishChanged(CommandAuthorizationChangeReason reason, string? commandId = null)
    {
        bool shouldDrain;
        long revision;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            revision = Interlocked.Increment(ref _revision);
            shouldDrain = _eventPublisher.Enqueue(
                AuthorizationChanged,
                new CommandAuthorizationChangedEventArgs(reason, revision, commandId));
        }

        SecurityDiagnostics.Write(
            _diagnostics,
            SecurityDiagnosticIds.CommandAuthorizationChanged,
            "Command authorization state changed.",
            HostDiagnosticSeverity.Info,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["commandId"] = commandId,
                ["changeReason"] = reason.ToString(),
                ["revision"] = revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });

        if (shouldDrain)
        {
            _eventPublisher.Drain(this);
        }
    }

    private CommandAuthorizationState Complete(
        CommandAuthorizationContext context,
        CommandAuthorizationDescriptor? descriptor,
        CommandAuthorizationState state)
    {
        WriteEvaluationDiagnostic(context, descriptor, state.Authorization);
        return state;
    }

    private AuthorizationResult Complete(
        CommandAuthorizationContext context,
        CommandAuthorizationDescriptor? descriptor,
        AuthorizationResult result)
    {
        WriteEvaluationDiagnostic(context, descriptor, result);
        return result;
    }

    private void WriteEvaluationDiagnostic(
        CommandAuthorizationContext context,
        CommandAuthorizationDescriptor? descriptor,
        AuthorizationResult result)
    {
        SecurityDiagnostics.Write(
            _diagnostics,
            SecurityDiagnosticIds.CommandAuthorizationEvaluated,
            "Command authorization completed.",
            result.Status == AuthorizationResultStatus.Failed
                ? HostDiagnosticSeverity.Error
                : result.Succeeded
                    ? HostDiagnosticSeverity.Info
                    : HostDiagnosticSeverity.Warning,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["commandId"] = context.CommandId,
                ["policyName"] = descriptor?.Policy.Name,
                ["resultStatus"] = result.Status.ToString(),
                ["failureKind"] = result.FailureKind.ToString(),
                ["contributionId"] = context.ContributionId ?? descriptor?.ContributionId,
            });
    }

    private void WriteLifecycleFailure(string operation, Exception exception)
    {
        SecurityDiagnostics.Write(
            _diagnostics,
            SecurityDiagnosticIds.CommandAuthorizationFailed,
            "Command authorization subscription lifecycle failed.",
            HostDiagnosticSeverity.Error,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["operation"] = operation,
                ["exceptionType"] = exception.GetType().FullName,
            });
    }

    private static void CaptureFailure(Action action, ICollection<Exception> failures)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
    }
}
