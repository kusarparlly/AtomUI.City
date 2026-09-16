using System.Collections.ObjectModel;
using AtomUI.City.Mvvm;

namespace AtomUI.City.Presentation;

/// <summary>
/// Represents validation visual state snapshot.
/// </summary>
public sealed class ValidationVisualStateSnapshot
{
    private ValidationVisualStateSnapshot(
        ValidationStatus status,
        IReadOnlyDictionary<string, IReadOnlyList<string>> errors,
        IReadOnlyDictionary<string, IReadOnlyList<ValidationMessage>> messages,
        Exception? exception)
    {
        Status = status;
        Errors = errors;
        Messages = messages;
        Exception = exception;
    }

    /// <summary>
    /// Gets status.
    /// </summary>
    public ValidationStatus Status { get; }

    /// <summary>
    /// Gets errors.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Errors { get; }

    /// <summary>
    /// Gets messages.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ValidationMessage>> Messages { get; }

    /// <summary>
    /// Gets exception.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Executes the from operation.
    /// </summary>
    public static ValidationVisualStateSnapshot From(ValidationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return new ValidationVisualStateSnapshot(
            scope.Status,
            Copy(scope.Errors),
            Copy(scope.Messages),
            scope.Exception);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<T>> Copy<T>(
        IReadOnlyDictionary<string, IReadOnlyList<T>> values)
    {
        var copy = new Dictionary<string, IReadOnlyList<T>>(StringComparer.Ordinal);

        foreach (var item in values)
        {
            copy[item.Key] = Array.AsReadOnly(item.Value.ToArray());
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<T>>(copy);
    }
}
