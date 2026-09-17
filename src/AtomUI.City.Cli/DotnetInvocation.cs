namespace AtomUI.City.Cli;

/// <summary>
/// Represents dotnet invocation.
/// </summary>
internal sealed class DotnetInvocation
{
    private DotnetInvocation(IReadOnlyList<string> arguments)
        : this(arguments, Directory.GetCurrentDirectory(), ciMode: false)
    {
    }

    private DotnetInvocation(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        bool ciMode)
    {
        Arguments = Array.AsReadOnly(arguments.ToArray());
        WorkingDirectory = workingDirectory;
        CiMode = ciMode;
    }

    /// <summary>
    /// Gets executable.
    /// </summary>
    public string Executable { get; } = "dotnet";

    /// <summary>
    /// Gets arguments.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets working directory.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// Gets ci mode.
    /// </summary>
    public bool CiMode { get; }

    internal static DotnetInvocation Create(string command, CliCommandLine commandLine)
    {
        return Create(command, commandLine, Directory.GetCurrentDirectory());
    }

    internal static DotnetInvocation Create(
        string command,
        CliCommandLine commandLine,
        string workingDirectory)
    {
        return Create(command, commandLine, workingDirectory, ciMode: false);
    }

    internal static DotnetInvocation Create(
        string command,
        CliCommandLine commandLine,
        string workingDirectory,
        bool ciMode)
    {
        var arguments = new List<string> { command };
        var project = commandLine.GetOptionValue("--project");
        if (!string.IsNullOrWhiteSpace(project))
        {
            arguments.Add(project);
        }

        var configuration = commandLine.GetOptionValue("--configuration");
        if (!string.IsNullOrWhiteSpace(configuration))
        {
            arguments.Add("--configuration");
            arguments.Add(configuration);
        }

        var framework = commandLine.GetOptionValue("--framework");
        if (!string.IsNullOrWhiteSpace(framework))
        {
            arguments.Add("--framework");
            arguments.Add(framework);
        }

        return new DotnetInvocation(arguments, workingDirectory, ciMode || commandLine.HasOption("--ci"));
    }
}
