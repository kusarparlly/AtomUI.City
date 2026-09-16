using System.Collections.ObjectModel;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Mvvm;

/// <summary>
/// Represents validation scope.
/// </summary>
public sealed class ValidationScope : IDisposable
{
    private readonly Dictionary<string, IReadOnlyList<string>> _errors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<ValidationMessage>> _messages = new(StringComparer.Ordinal);
    private readonly IHostDiagnostics? _diagnostics;
    private readonly object _syncRoot = new();
    private Guid? _ownerScopeId;
    private bool _disposed;
    private ValidationStatus _status = ValidationStatus.Valid;
    private Exception? _exception;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationScope"/> type.
    /// </summary>
    public ValidationScope(IHostDiagnostics? diagnostics = null)
    {
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Occurs when validation changed.
    /// </summary>
    public event EventHandler<ValidationChangedEventArgs>? ValidationChanged;

    /// <summary>
    /// Represents the status value.
    /// </summary>
    public ValidationStatus Status
    {
        get
        {
            lock (_syncRoot)
            {
                return _status;
            }
        }

        private set
        {
            lock (_syncRoot)
            {
                _status = value;
            }
        }
    }

    /// <summary>
    /// Represents the is disposed value.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_syncRoot)
            {
                return _disposed;
            }
        }
    }

    /// <summary>
    /// Represents the errors value.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors
    {
        get
        {
            lock (_syncRoot)
            {
                return new ReadOnlyDictionary<string, IReadOnlyList<string>>(
                    _errors.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
            }
        }
    }

    /// <summary>
    /// Represents the messages value.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ValidationMessage>> Messages
    {
        get
        {
            lock (_syncRoot)
            {
                return new ReadOnlyDictionary<string, IReadOnlyList<ValidationMessage>>(
                    _messages.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
            }
        }
    }

    /// <summary>
    /// Represents the exception value.
    /// </summary>
    public Exception? Exception
    {
        get
        {
            lock (_syncRoot)
            {
                return _exception;
            }
        }

        private set
        {
            lock (_syncRoot)
            {
                _exception = value;
            }
        }
    }

    /// <summary>
    /// Executes the bind to operation.
    /// </summary>
    public void BindTo(IActivationScope activationScope)
    {
        ArgumentNullException.ThrowIfNull(activationScope);
        ThrowIfDisposed();

        lock (_syncRoot)
        {
            _ownerScopeId = activationScope.Id;
        }

        activationScope.Add(new DelegateDisposable(Cancel));
    }

    /// <summary>
    /// Executes the set invalid operation.
    /// </summary>
    public void SetInvalid(
        string key,
        string message,
        string? messageKey = null,
        IReadOnlyList<object?>? messageArguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        SetMessages(
            key,
            [
                new ValidationMessage(
                    key,
                    message,
                    messageKey,
                    messageArguments),
            ]);
    }

    /// <summary>
    /// Executes the set messages operation.
    /// </summary>
    public void SetMessages(
        string? key,
        IEnumerable<ValidationMessage?> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ThrowIfDisposed();

        var normalizedKey = NormalizeKey(key);
        var uniqueMessages = new List<ValidationMessage>();
        var seen = new HashSet<(string Message, string? MessageKey)>();

        foreach (var message in messages)
        {
            if (message is null)
            {
                throw new ArgumentException("Validation messages cannot contain null.", nameof(messages));
            }

            if (seen.Add((message.Message, message.MessageKey)))
            {
                uniqueMessages.Add(message);
            }
        }

        ValidationChangedEventArgs args;

        lock (_syncRoot)
        {
            if (uniqueMessages.Count == 0)
            {
                _errors.Remove(normalizedKey);
                _messages.Remove(normalizedKey);
            }
            else
            {
                _errors[normalizedKey] = Array.AsReadOnly(uniqueMessages.Select(message => message.Message).ToArray());
                _messages[normalizedKey] = Array.AsReadOnly(uniqueMessages.ToArray());
            }

            _exception = null;
            _status = _messages.Count == 0 ? ValidationStatus.Valid : ValidationStatus.Invalid;
            args = CaptureChangedEventArgs(normalizedKey);
        }

        RaiseChanged(args);
    }

    /// <summary>
    /// Executes the set pending operation.
    /// </summary>
    public void SetPending()
    {
        ThrowIfDisposed();

        ValidationChangedEventArgs args;

        lock (_syncRoot)
        {
            _exception = null;
            _status = ValidationStatus.Pending;
            args = CaptureChangedEventArgs(string.Empty);
        }

        RaiseChanged(args);
    }

    /// <summary>
    /// Executes the set failed operation.
    /// </summary>
    public void SetFailed(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ThrowIfDisposed();

        ValidationChangedEventArgs args;

        lock (_syncRoot)
        {
            _errors.Clear();
            _messages.Clear();
            _exception = exception;
            _status = ValidationStatus.Failed;
            args = CaptureChangedEventArgs(string.Empty);
        }

        _diagnostics?.Write(new HostDiagnosticRecord(
            MvvmDiagnosticIds.ValidationFailed,
            $"Validation scope failed: {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = new Dictionary<string, string?>
            {
                ["ownerScopeId"] = _ownerScopeId?.ToString(),
                ["exceptionType"] = exception.GetType().FullName,
            }
        });

        RaiseChanged(args);
    }

    /// <summary>
    /// Executes the cancel operation.
    /// </summary>
    public void Cancel()
    {
        ThrowIfDisposed();

        ValidationChangedEventArgs args;

        lock (_syncRoot)
        {
            _exception = null;
            _status = ValidationStatus.Canceled;
            args = CaptureChangedEventArgs(string.Empty);
        }

        RaiseChanged(args);
    }

    /// <summary>
    /// Executes the dispose operation.
    /// </summary>
    public void Dispose()
    {
        lock (_syncRoot)
        {
            _disposed = true;
        }
    }

    private ValidationChangedEventArgs CaptureChangedEventArgs(string key)
    {
        _errors.TryGetValue(key, out var errors);
        _messages.TryGetValue(key, out var messages);

        return new ValidationChangedEventArgs(
            key,
            _status,
            errors ?? Array.Empty<string>(),
            messages ?? Array.Empty<ValidationMessage>(),
            _ownerScopeId);
    }

    private void RaiseChanged(ValidationChangedEventArgs args)
    {
        ValidationChanged?.Invoke(this, args);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }
    }

    private static string NormalizeKey(string? key)
    {
        return key is null ? string.Empty : key;
    }

    private sealed class DelegateDisposable : IDisposable
    {
        private readonly Action _dispose;
        private bool _disposed;

        public DelegateDisposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _dispose();
        }
    }
}
