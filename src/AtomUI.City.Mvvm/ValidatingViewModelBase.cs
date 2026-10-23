using System.Collections;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Mvvm;

/// <summary>
/// Provides AOT-safe observable validation state for ViewModels whose validators
/// are supplied by the City source generator.
/// </summary>
public abstract class ValidatingViewModelBase : ViewModelBase, INotifyDataErrorInfo
{
    private readonly IHostDiagnostics? _diagnostics;
    private readonly Dictionary<string, IReadOnlyList<string>> _errors = new(StringComparer.Ordinal);
    private readonly object _validationSyncRoot = new();
    private Exception? _validationException;
    private ValidationStatus _validationStatus = ValidationStatus.Valid;

    /// <summary>
    /// Initializes a new validation-capable ViewModel.
    /// </summary>
    protected ValidatingViewModelBase(IHostDiagnostics? diagnostics = null)
        : base(diagnostics)
    {
        _diagnostics = diagnostics;
    }

    /// <inheritdoc />
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <inheritdoc />
    public bool HasErrors
    {
        get
        {
            lock (_validationSyncRoot)
            {
                return _errors.Count > 0;
            }
        }
    }

    /// <summary>
    /// Gets the current generated-validation status.
    /// </summary>
    public ValidationStatus ValidationStatus
    {
        get
        {
            lock (_validationSyncRoot)
            {
                return _validationStatus;
            }
        }
    }

    /// <summary>
    /// Gets the exception produced by a validation rule, if validation failed.
    /// </summary>
    public Exception? ValidationException
    {
        get
        {
            lock (_validationSyncRoot)
            {
                return _validationException;
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable GetErrors(string? propertyName)
    {
        lock (_validationSyncRoot)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return _errors
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .SelectMany(pair => pair.Value)
                    .ToArray();
            }

            return _errors.TryGetValue(propertyName, out var errors)
                ? errors.ToArray()
                : Array.Empty<string>();
        }
    }

    /// <summary>
    /// Executes every generated property validator in deterministic order.
    /// </summary>
    public void ValidateAllProperties()
    {
        ThrowIfDisposed();
        ValidateAllGeneratedProperties();
    }

    /// <summary>
    /// Updates a property and optionally executes its generated validator.
    /// </summary>
    protected bool SetProperty<T>(
        ref T field,
        T newValue,
        bool validate,
        [CallerMemberName] string? propertyName = null)
    {
        return SetProperty(ref field, newValue, EqualityComparer<T>.Default, validate, propertyName);
    }

    /// <summary>
    /// Updates a property with a comparer and optionally executes its generated validator.
    /// </summary>
    protected bool SetProperty<T>(
        ref T field,
        T newValue,
        IEqualityComparer<T> comparer,
        bool validate,
        [CallerMemberName] string? propertyName = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var changed = base.SetProperty(ref field, newValue, comparer, propertyName);
        if (changed && validate)
        {
            ValidateProperty(propertyName, newValue);
        }

        return changed;
    }

    /// <summary>
    /// Validates one property through the generated dispatch implementation.
    /// </summary>
    protected void ValidateProperty(string propertyName, object? value)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var errors = new List<string>();
        try
        {
            ValidateGeneratedProperty(propertyName, value, errors);
            CommitValidation(propertyName, errors, exception: null);
        }
        catch (Exception exception)
        {
            CommitValidation(propertyName, [], exception);
            MvvmDiagnostics.Write(_diagnostics, new HostDiagnosticRecord(
                MvvmDiagnosticIds.ValidationFailed,
                $"Generated validation failed for '{GetType().FullName}.{propertyName}': {exception.Message}",
                HostDiagnosticSeverity.Error)
            {
                Context = new Dictionary<string, string?>
                {
                    ["viewModelType"] = GetType().FullName,
                    ["propertyName"] = propertyName,
                    ["exceptionType"] = exception.GetType().FullName,
                }
            });
        }
    }

    /// <summary>
    /// Adds a validation message when a statically constructed DataAnnotations rule rejects a value.
    /// </summary>
    protected static void ValidateAttribute(
        ICollection<string> errors,
        object? value,
        string displayName,
        ValidationAttribute attribute)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(attribute);

        if (!attribute.IsValid(value))
        {
            errors.Add(attribute.FormatErrorMessage(displayName));
        }
    }

    /// <summary>
    /// Adds a directly returned custom validation result.
    /// </summary>
    protected static void ValidateResult(
        ICollection<string> errors,
        ValidationResult? result)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (result != ValidationResult.Success)
        {
            errors.Add(result?.ErrorMessage ?? "Validation failed.");
        }
    }

    /// <summary>
    /// Receives the generated property dispatch implementation.
    /// </summary>
    protected virtual void ValidateGeneratedProperty(
        string propertyName,
        object? value,
        ICollection<string> errors)
    {
    }

    /// <summary>
    /// Receives the generated deterministic full-validation implementation.
    /// </summary>
    protected virtual void ValidateAllGeneratedProperties()
    {
    }

    private void CommitValidation(
        string propertyName,
        IReadOnlyList<string> errors,
        Exception? exception)
    {
        var normalized = errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        bool errorsChanged;
        bool hasErrorsChanged;
        bool statusChanged;
        bool exceptionChanged;

        lock (_validationSyncRoot)
        {
            var hadErrors = _errors.Count > 0;
            var previousStatus = _validationStatus;
            var previousException = _validationException;
            var previous = _errors.TryGetValue(propertyName, out var existing)
                ? existing
                : Array.Empty<string>();
            errorsChanged = !previous.SequenceEqual(normalized, StringComparer.Ordinal);

            if (normalized.Length == 0)
            {
                _errors.Remove(propertyName);
            }
            else
            {
                _errors[propertyName] = Array.AsReadOnly(normalized);
            }

            _validationException = exception;
            _validationStatus = exception is not null
                ? ValidationStatus.Failed
                : _errors.Count > 0
                    ? ValidationStatus.Invalid
                    : ValidationStatus.Valid;

            hasErrorsChanged = hadErrors != (_errors.Count > 0);
            statusChanged = previousStatus != _validationStatus;
            exceptionChanged = !ReferenceEquals(previousException, _validationException);
        }

        if (errorsChanged)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
        if (hasErrorsChanged)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasErrors)));
        }
        if (statusChanged)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(ValidationStatus)));
        }
        if (exceptionChanged)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(ValidationException)));
        }
    }
}
