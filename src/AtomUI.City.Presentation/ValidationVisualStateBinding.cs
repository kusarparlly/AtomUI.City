using AtomUI.City.Core.Diagnostics;
using AtomUI.City.Mvvm;
using AtomUI.City.Core.Threading;

namespace AtomUI.City.Presentation;

/// <summary>
/// Represents validation visual state binding.
/// </summary>
public sealed class ValidationVisualStateBinding
{
    private readonly IUiDispatcher _dispatcher;
    private readonly IHostDiagnostics? _diagnostics;

    /// <summary>
    /// Initializes a new instance of the <c>ValidationVisualStateBinding</c> type.
    /// </summary>
    public ValidationVisualStateBinding(IUiDispatcher dispatcher)
        : this(dispatcher, diagnostics: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <c>ValidationVisualStateBinding</c> type.
    /// </summary>
    public ValidationVisualStateBinding(
        IUiDispatcher dispatcher,
        IHostDiagnostics? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        _dispatcher = dispatcher;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Executes the apply async operation.
    /// </summary>
    public async ValueTask ApplyAsync(
        ValidationScope scope,
        IValidationVisualStateTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(target);

        var snapshot = ValidationVisualStateSnapshot.From(scope);

        try
        {
            await _dispatcher
                .InvokeAsync(
                    () => target.ApplyValidationState(snapshot),
                    cancellationToken)
                .ConfigureAwait(false);

            WriteAppliedDiagnostic(snapshot, target);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            WriteFailedDiagnostic(snapshot, target, exception);
            throw;
        }
    }

    private void WriteAppliedDiagnostic(
        ValidationVisualStateSnapshot snapshot,
        IValidationVisualStateTarget target)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.ValidationVisualStateApplied,
            $"Presentation validation visual state applied status '{snapshot.Status}' with keys '{FormatKeys(snapshot)}'.",
            HostDiagnosticSeverity.Info)
        {
            Context = CreateDiagnosticContext(snapshot, target, exception: null),
        });
    }

    private void WriteFailedDiagnostic(
        ValidationVisualStateSnapshot snapshot,
        IValidationVisualStateTarget target,
        Exception exception)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.ValidationVisualStateApplyFailed,
            $"Presentation validation visual state failed to apply status '{snapshot.Status}' with keys '{FormatKeys(snapshot)}': {exception.Message}",
            HostDiagnosticSeverity.Error)
        {
            Context = CreateDiagnosticContext(snapshot, target, exception),
        });
    }

    private static IReadOnlyDictionary<string, string?> CreateDiagnosticContext(
        ValidationVisualStateSnapshot snapshot,
        IValidationVisualStateTarget target,
        Exception? exception)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["status"] = snapshot.Status.ToString(),
            ["keys"] = FormatKeys(snapshot),
            ["messageCount"] = snapshot.Messages.Values.Sum(static messages => messages.Count).ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["targetType"] = target.GetType().FullName,
            ["error"] = exception?.GetType().FullName,
        };
    }

    private static string FormatKeys(ValidationVisualStateSnapshot snapshot)
    {
        return snapshot.Errors.Count == 0 ? "<none>" : string.Join(", ", snapshot.Errors.Keys);
    }
}
