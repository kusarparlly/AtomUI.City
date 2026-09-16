namespace AtomUI.City.EventBus;

/// <summary>
/// Represents event bus diagnostics options.
/// </summary>
public sealed class EventBusDiagnosticsOptions
{
    /// <summary>
    /// Represents the default memory buffer capacity value.
    /// </summary>
    public const int DefaultMemoryBufferCapacity = 2048;
    /// <summary>
    /// Represents the default maximum payload field count value.
    /// </summary>
    public const int DefaultMaximumPayloadFieldCount = 16;
    /// <summary>
    /// Represents the default maximum payload value length value.
    /// </summary>
    public const int DefaultMaximumPayloadValueLength = 512;

    private double _traceSamplingRate = 1d;
    private int _maximumPayloadFieldCount = DefaultMaximumPayloadFieldCount;
    private int _maximumPayloadValueLength = DefaultMaximumPayloadValueLength;

    /// <summary>
    /// Gets default.
    /// </summary>
    public static EventBusDiagnosticsOptions Default { get; } = new();

    /// <summary>
    /// Represents the trace sampling rate value.
    /// </summary>
    public double TraceSamplingRate
    {
        get => _traceSamplingRate;
        init
        {
            if (double.IsNaN(value) || value is < 0d or > 1d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "EventBus diagnostic trace sampling rate must be between zero and one.");
            }

            _traceSamplingRate = value;
        }
    }

    /// <summary>
    /// Gets or sets enable payload projection.
    /// </summary>
    public bool EnablePayloadProjection { get; init; }

    /// <summary>
    /// Represents the maximum payload field count value.
    /// </summary>
    public int MaximumPayloadFieldCount
    {
        get => _maximumPayloadFieldCount;
        init
        {
            if (value is <= 0 or > 64)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "EventBus payload diagnostic field count must be between 1 and 64.");
            }

            _maximumPayloadFieldCount = value;
        }
    }

    /// <summary>
    /// Represents the maximum payload value length value.
    /// </summary>
    public int MaximumPayloadValueLength
    {
        get => _maximumPayloadValueLength;
        init
        {
            if (value is <= 0 or > 4096)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "EventBus payload diagnostic value length must be between 1 and 4096.");
            }

            _maximumPayloadValueLength = value;
        }
    }
}
