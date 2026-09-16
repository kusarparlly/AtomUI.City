namespace AtomUI.City.Cli;

/// <summary>
/// Represents cli execution environment.
/// </summary>
public sealed class CliExecutionEnvironment
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CliExecutionEnvironment"/> type.
    /// </summary>
    public CliExecutionEnvironment(
        string workingDirectory,
        bool isCi = false,
        bool isNonInteractive = false,
        bool isStdinAvailable = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        WorkingDirectory = workingDirectory;
        IsCi = isCi;
        IsStdinAvailable = isStdinAvailable;
        IsNonInteractive = isNonInteractive || isCi || !isStdinAvailable;
    }

    /// <summary>
    /// Gets working directory.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// Gets a value indicating whether is ci.
    /// </summary>
    public bool IsCi { get; }

    /// <summary>
    /// Gets a value indicating whether is non interactive.
    /// </summary>
    public bool IsNonInteractive { get; }

    /// <summary>
    /// Gets a value indicating whether is stdin available.
    /// </summary>
    public bool IsStdinAvailable { get; }
}
