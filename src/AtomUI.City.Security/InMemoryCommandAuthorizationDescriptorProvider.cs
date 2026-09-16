using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Security;

/// <summary>
/// Represents in memory command authorization descriptor provider.
/// </summary>
public sealed class InMemoryCommandAuthorizationDescriptorProvider : ICommandAuthorizationDescriptorProvider
{
    private readonly Dictionary<string, CommandAuthorizationDescriptor> _descriptors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _revokedContributions = new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();
    private readonly OrderedEventPublisher<CommandAuthorizationChangedEventArgs> _eventPublisher;
    private long _revision;

    /// <summary>
    /// Initializes a new instance of the <c>InMemoryCommandAuthorizationDescriptorProvider</c> type.
    /// </summary>
    public InMemoryCommandAuthorizationDescriptorProvider()
    {
        _eventPublisher = new OrderedEventPublisher<CommandAuthorizationChangedEventArgs>(
            diagnostics: null,
            SecurityDiagnosticIds.CommandAuthorizationObserverFailed);
    }

    /// <summary>
    /// Initializes a new instance of the <c>InMemoryCommandAuthorizationDescriptorProvider</c> type.
    /// </summary>
    public InMemoryCommandAuthorizationDescriptorProvider(IHostDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _eventPublisher = new OrderedEventPublisher<CommandAuthorizationChangedEventArgs>(
            diagnostics,
            SecurityDiagnosticIds.CommandAuthorizationObserverFailed);
    }

    /// <summary>
    /// Occurs when descriptor changed.
    /// </summary>
    public event EventHandler<CommandAuthorizationChangedEventArgs>? DescriptorChanged;

    /// <summary>
    /// Represents the revision value.
    /// </summary>
    public long Revision
    {
        get
        {
            lock (_syncRoot)
            {
                return _revision;
            }
        }
    }

    /// <summary>
    /// Executes the add operation.
    /// </summary>
    public bool Add(CommandAuthorizationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        long revision;
        CommandAuthorizationChangedEventArgs args;
        bool shouldDrain;

        lock (_syncRoot)
        {
            if (!string.IsNullOrWhiteSpace(descriptor.ContributionId)
                && _revokedContributions.Contains(descriptor.ContributionId))
            {
                return false;
            }

            if (_descriptors.ContainsKey(descriptor.CommandId))
            {
                return false;
            }

            _descriptors.Add(descriptor.CommandId, descriptor);
            revision = ++_revision;
            args = new CommandAuthorizationChangedEventArgs(
                CommandAuthorizationChangeReason.DescriptorChanged,
                revision,
                descriptor.CommandId);
            shouldDrain = _eventPublisher.Enqueue(DescriptorChanged, args);
        }

        if (shouldDrain)
        {
            _eventPublisher.Drain(this);
        }

        return true;
    }

    /// <summary>
    /// Executes the remove operation.
    /// </summary>
    public bool Remove(string commandId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        long revision;
        CommandAuthorizationChangedEventArgs args;
        bool shouldDrain;

        lock (_syncRoot)
        {
            if (!_descriptors.Remove(commandId))
            {
                return false;
            }

            revision = ++_revision;
            args = new CommandAuthorizationChangedEventArgs(
                CommandAuthorizationChangeReason.DescriptorChanged,
                revision,
                commandId);
            shouldDrain = _eventPublisher.Enqueue(DescriptorChanged, args);
        }

        if (shouldDrain)
        {
            _eventPublisher.Drain(this);
        }

        return true;
    }

    /// <summary>
    /// Executes the remove by contribution operation.
    /// </summary>
    public int RemoveByContribution(string contributionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);
        string[] commandIds;
        long revision;
        CommandAuthorizationChangedEventArgs args;
        bool shouldDrain;

        lock (_syncRoot)
        {
            _revokedContributions.Add(contributionId);
            commandIds = _descriptors
                .Where(pair => string.Equals(
                    pair.Value.ContributionId,
                    contributionId,
                    StringComparison.Ordinal))
                .Select(static pair => pair.Key)
                .ToArray();

            foreach (var commandId in commandIds)
            {
                _descriptors.Remove(commandId);
            }

            if (commandIds.Length == 0)
            {
                return 0;
            }

            revision = ++_revision;
            args = new CommandAuthorizationChangedEventArgs(
                CommandAuthorizationChangeReason.DescriptorChanged,
                revision);
            shouldDrain = _eventPublisher.Enqueue(DescriptorChanged, args);
        }

        if (shouldDrain)
        {
            _eventPublisher.Drain(this);
        }

        return commandIds.Length;
    }

    /// <summary>
    /// Executes the get descriptor async operation.
    /// </summary>
    public ValueTask<CommandAuthorizationDescriptor?> GetDescriptorAsync(
        CommandAuthorizationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _descriptors.TryGetValue(context.CommandId, out var descriptor);

            return ValueTask.FromResult<CommandAuthorizationDescriptor?>(descriptor);
        }
    }
}
