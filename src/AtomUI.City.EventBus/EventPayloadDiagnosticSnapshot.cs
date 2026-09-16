using System.Collections.ObjectModel;

namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event payload diagnostic snapshot.
/// </summary>
public sealed class EventPayloadDiagnosticSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EventPayloadDiagnosticSnapshot"/> type.
    /// </summary>
    public EventPayloadDiagnosticSnapshot(
        IReadOnlyDictionary<string, string?> fields,
        string? schemaVersion = null,
        long? sizeEstimate = null)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count > 64)
        {
            throw new ArgumentException("A payload diagnostic snapshot cannot contain more than 64 fields.", nameof(fields));
        }

        if (string.IsNullOrWhiteSpace(schemaVersion) && schemaVersion is not null)
        {
            throw new ArgumentException("Payload diagnostic schema version cannot be empty or whitespace.", nameof(schemaVersion));
        }

        if (schemaVersion?.Length > 128)
        {
            throw new ArgumentException("Payload diagnostic schema version cannot exceed 128 characters.", nameof(schemaVersion));
        }

        if (sizeEstimate < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeEstimate), sizeEstimate, "Payload size estimate cannot be negative.");
        }

        var snapshot = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field.Key);
            if (field.Key.Length > 128)
            {
                throw new ArgumentException("Payload diagnostic field names cannot exceed 128 characters.", nameof(fields));
            }

            if (field.Key.Any(char.IsControl))
            {
                throw new ArgumentException("Payload diagnostic field names cannot contain control characters.", nameof(fields));
            }

            if (field.Value?.Length > 4096)
            {
                throw new ArgumentException("Payload diagnostic field values cannot exceed 4096 characters.", nameof(fields));
            }

            snapshot.Add(field.Key, field.Value);
        }

        Fields = new ReadOnlyDictionary<string, string?>(snapshot);
        SchemaVersion = schemaVersion;
        SizeEstimate = sizeEstimate;
    }

    /// <summary>
    /// Gets fields.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Fields { get; }

    /// <summary>
    /// Gets schema version.
    /// </summary>
    public string? SchemaVersion { get; }

    /// <summary>
    /// Gets size estimate.
    /// </summary>
    public long? SizeEstimate { get; }
}

/// <summary>
/// Defines the contract for ievent payload diagnostic projector&lt;tevent&gt;.
/// </summary>
public interface IEventPayloadDiagnosticProjector<in TEvent>
{
    /// <summary>
    /// Executes the project operation.
    /// </summary>
    EventPayloadDiagnosticSnapshot Project(TEvent eventData);
}

/// <summary>
/// Represents event payload diagnostic projector descriptor.
/// </summary>
public class EventPayloadDiagnosticProjectorDescriptor
{
    private readonly Func<object, EventPayloadDiagnosticSnapshot> _project;

    private protected EventPayloadDiagnosticProjectorDescriptor(
        Type eventType,
        Func<object, EventPayloadDiagnosticSnapshot> project)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        ArgumentNullException.ThrowIfNull(project);
        EventType = eventType;
        _project = project;
    }

    /// <summary>
    /// Gets event type.
    /// </summary>
    public Type EventType { get; }

    /// <summary>
    /// Executes the create&lt;tevent&gt; operation.
    /// </summary>
    public static EventPayloadDiagnosticProjectorDescriptor Create<TEvent>(
        IEventPayloadDiagnosticProjector<TEvent> projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        return new EventPayloadDiagnosticProjectorDescriptor(
            typeof(TEvent),
            eventData => projector.Project((TEvent)eventData));
    }

    internal EventPayloadDiagnosticSnapshot Project(object eventData)
    {
        return _project(eventData) ??
               throw new InvalidOperationException("An event payload diagnostic projector returned null.");
    }
}

internal sealed class EventPayloadDiagnosticProjectorRegistration<TEvent, TProjector>
    : EventPayloadDiagnosticProjectorDescriptor
    where TProjector : class, IEventPayloadDiagnosticProjector<TEvent>
{
    public EventPayloadDiagnosticProjectorRegistration(TProjector projector)
        : base(
            typeof(TEvent),
            eventData => projector.Project((TEvent)eventData))
    {
        ArgumentNullException.ThrowIfNull(projector);
    }
}
