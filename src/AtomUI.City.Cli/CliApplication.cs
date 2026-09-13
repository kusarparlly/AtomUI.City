using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using AtomUI.City.PluginSystem;
using AtomUI.City.Templates;

namespace AtomUI.City.Cli;

public static class CliApplication
{
    private const int ProcessOutputSummaryLimit = 4096;
    private const string ProcessOutputTruncationSuffix = "\n[truncated]";

    private static readonly string[] UsageLines =
    [
        "atomui city doctor",
        "atomui city new app <AppName>",
        "atomui city generate module <Name>",
        "atomui city generate page <Name> --route <Path>",
        "atomui city generate test <Name>",
        "atomui city generate config <Name>",
        "atomui city generate localization <Name>",
        "atomui city build",
        "atomui city test",
        "atomui city inspect workspace",
        "atomui city plugin list",
        "atomui city plugin inspect <Path>",
        "atomui city docs check",
        "atomui city tests check",
    ];

    public static async ValueTask<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CliExecutionEnvironment? environment = null,
        CancellationToken cancellationToken = default)
    {
        return await RunAsync(args, output, error, environment, cancellationToken, ProcessRunner.RunAsync).ConfigureAwait(false);
    }

    internal static async ValueTask<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CliExecutionEnvironment? environment,
        CancellationToken cancellationToken,
        Func<DotnetInvocation, CancellationToken, ValueTask<ProcessRunResult>> processRunner)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(processRunner);

        var commandLine = CliCommandLine.Parse(args);
        var baseEnvironment = environment ?? CreateDefaultEnvironment();
        var workingDirectory = commandLine.GetOptionValue("--working-directory");
        var executionEnvironment = CreateExecutionEnvironment(commandLine, baseEnvironment, workingDirectory);

        if (commandLine.Positionals.Count == 0 || commandLine.Positionals[0] != "city")
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    "atomui city",
                    CliEnvelope.Failed(
                        "atomui city",
                        CliExitCodes.ArgumentError,
                        CliDiagnostic.Error(
                            "AUCCLI0001",
                            "Command must start with 'city'.",
                            commandLine.Positionals.Count == 0 ? null : commandLine.Positionals[0],
                            commandLine.Positionals.Count == 0 ? null : 0)))
                .ConfigureAwait(false);
        }

        if (commandLine.Diagnostics.Count > 0)
        {
            var command = BuildCommandName(commandLine);
            return await WriteAsync(
                    output,
                    commandLine,
                    command,
                    CliEnvelope.FailedWithData(
                        command,
                        CliExitCodes.ArgumentError,
                        CreateUsageData(),
                        commandLine.Diagnostics.ToArray()))
                .ConfigureAwait(false);
        }

        if (commandLine.Positionals.Count == 1)
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    "atomui city",
                    CliEnvelope.FailedWithData(
                        "atomui city",
                        CliExitCodes.ArgumentError,
                        CreateUsageData(),
                        CliDiagnostic.Error(
                            "AUCCLI0003",
                            "Command is required.",
                            "city",
                            1)))
                .ConfigureAwait(false);
        }

        return await DispatchAsync(commandLine, executionEnvironment, output, cancellationToken, processRunner).ConfigureAwait(false);
    }

    private static async ValueTask<int> DispatchAsync(
        CliCommandLine commandLine,
        CliExecutionEnvironment environment,
        TextWriter output,
        CancellationToken cancellationToken,
        Func<DotnetInvocation, CancellationToken, ValueTask<ProcessRunResult>> processRunner)
    {
        var positionals = commandLine.Positionals;
        var command = positionals.Count > 1 ? positionals[1] : "doctor";

        return command switch
        {
            "doctor" => await DoctorAsync(commandLine, environment, output).ConfigureAwait(false),
            "new" => await NewAsync(commandLine, environment, output, cancellationToken).ConfigureAwait(false),
            "generate" => await GenerateAsync(commandLine, environment, output, cancellationToken).ConfigureAwait(false),
            "build" or "test" or "pack" or "publish" => await DotnetCommandAsync(command, commandLine, environment, output, cancellationToken, processRunner).ConfigureAwait(false),
            "inspect" => await InspectAsync(commandLine, environment, output).ConfigureAwait(false),
            "plugin" => await PluginAsync(commandLine, environment, output, cancellationToken).ConfigureAwait(false),
            "docs" when positionals.Count > 2 && positionals[2] == "check" => await GateCheckAsync("docs", commandLine, environment, output).ConfigureAwait(false),
            "tests" when positionals.Count > 2 && positionals[2] == "check" => await GateCheckAsync("tests", commandLine, environment, output).ConfigureAwait(false),
            "explain" => await ExplainAsync(commandLine, output).ConfigureAwait(false),
            "plan" => await GenericPlanAsync(commandLine, output).ConfigureAwait(false),
            "apply" => await ApplyAsync(commandLine, environment, output).ConfigureAwait(false),
            _ => await WriteAsync(
                    output,
                    commandLine,
                    "atomui city " + command,
                    CliEnvelope.FailedWithData(
                        "atomui city " + command,
                        CliExitCodes.ArgumentError,
                        CreateUsageData(),
                        CliDiagnostic.Error(
                            "AUCCLI0002",
                            $"Unknown command '{command}'.",
                            command,
                            1)))
                .ConfigureAwait(false),
        };
    }

    private static async ValueTask<int> DoctorAsync(
        CliCommandLine commandLine,
        CliExecutionEnvironment environment,
        TextWriter output)
    {
        var solutionPath = Path.Combine(environment.WorkingDirectory, "AtomUICity.slnx");
        var data = new Dictionary<string, object?>
        {
            ["workingDirectory"] = environment.WorkingDirectory,
            ["solutionExists"] = File.Exists(solutionPath),
            ["docsDirectoryExists"] = Directory.Exists(Path.Combine(environment.WorkingDirectory, "docs")),
            ["testsDirectoryExists"] = Directory.Exists(Path.Combine(environment.WorkingDirectory, "tests")),
        };

        return await WriteAsync(
                output,
                commandLine,
                "atomui city doctor",
                CliEnvelope.Succeeded("atomui city doctor", data))
            .ConfigureAwait(false);
    }

    private static async ValueTask<int> NewAsync(
        CliCommandLine commandLine,
        CliExecutionEnvironment environment,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var positionals = commandLine.Positionals;
        if (positionals.Count < 4 || positionals[2] != "app")
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    "atomui city new app",
                    CliEnvelope.Failed(
                        "atomui city new app",
                        CliExitCodes.ArgumentError,
                        CliDiagnostic.Error("AUCCLI0101", "AppName is required.")))
                .ConfigureAwait(false);
        }

        var appName = positionals[3];
        if (!IsValidIdentifier(appName))
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    "atomui city new app",
                    CliEnvelope.Failed(
                        "atomui city new app",
                        CliExitCodes.ArgumentError,
                        CliDiagnostic.Error(
                            "AUCCLI0104",
                            "AppName must be a valid identifier.",
                            appName,
                            3)))
                .ConfigureAwait(false);
        }

        var rootNamespace = commandLine.GetOptionValue("--namespace") ?? appName;
        if (rootNamespace.Equals("AtomUI.City", StringComparison.Ordinal) ||
            rootNamespace.StartsWith("AtomUI.City.", StringComparison.Ordinal))
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    "atomui city new app",
                    CliEnvelope.Failed(
                        "atomui city new app",
                        CliExitCodes.ArgumentError,
                        CliDiagnostic.Error("AUCCLI0102", "Root namespace must not start with 'AtomUI.City'.")))
                .ConfigureAwait(false);
        }

        if (commandLine.HasOption("--use-aot") && commandLine.HasOption("--use-dynamic-plugins"))
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    "atomui city new app",
                    CliEnvelope.Failed(
                        "atomui city new app",
                        CliExitCodes.ArgumentError,
                        CliDiagnostic.Error("AUCCLI0103", "--use-aot cannot be combined with --use-dynamic-plugins by default.")))
                .ConfigureAwait(false);
        }

        var outputPath = commandLine.GetOptionValue("--output") ?? environment.WorkingDirectory;
        string resolvedOutputPath;
        try
        {
            resolvedOutputPath = Path.GetFullPath(outputPath, environment.WorkingDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    "atomui city new app",
                    CliEnvelope.Failed(
                        "atomui city new app",
                        CliExitCodes.ArgumentError,
                        CliDiagnostic.Error("AUCTPL0001", "Output path is invalid.", outputPath)))
                .ConfigureAwait(false);
        }

        var options = new ApplicationTemplateOptions
        {
            AppName = appName,
            RootNamespace = rootNamespace,
            OutputPath = resolvedOutputPath,
            TargetFramework = commandLine.GetOptionValue("--target-framework") ?? "net10.0",
            IncludeTests = !commandLine.HasOption("--no-tests"),
            IncludeSample = commandLine.HasOption("--sample"),
            UseAot = commandLine.HasOption("--use-aot"),
            UseDynamicPlugins = commandLine.HasOption("--use-dynamic-plugins"),
        };
        var optionDiagnostics = options.Validate();
        if (optionDiagnostics.Count > 0)
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    "atomui city new app",
                    CliEnvelope.Failed(
                        "atomui city new app",
                        CliExitCodes.ArgumentError,
                        optionDiagnostics.Select(ToCliTemplateDiagnostic).ToArray()))
                .ConfigureAwait(false);
        }

        var renderer = new ApplicationTemplateRenderer();
        var plan = renderer.CreatePlan(options);
        var artifacts = CreateArtifacts(plan);

        if (!commandLine.HasOption("--dry-run"))
        {
            var conflict = plan.Changes
                .Select(change => change.Path)
                .FirstOrDefault(relativePath => File.Exists(ResolveTemplatePath(options.OutputPath, relativePath)));
            if (conflict is not null)
            {
                return await WriteAsync(
                        output,
                        commandLine,
                        "atomui city new app",
                        CliEnvelope.FailedWithData(
                            "atomui city new app",
                            CliExitCodes.ArgumentError,
                            new Dictionary<string, object?>
                            {
                                ["plan"] = plan,
                                ["artifacts"] = artifacts,
                                ["conflict"] = conflict,
                            },
                            CliDiagnostic.Error(
                                "AUCCLI0105",
                                $"Target already exists: '{conflict}'.",
                                conflict)))
                    .ConfigureAwait(false);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return await WriteAsync(
                        output,
                        commandLine,
                        "atomui city new app",
                        CliEnvelope.FailedWithData(
                            "atomui city new app",
                            CliExitCodes.Failure,
                            new Dictionary<string, object?>
                            {
                                ["plan"] = plan,
                                ["artifacts"] = artifacts,
                            },
                            CliDiagnostic.Error(
                                "AUCCLI0106",
                                "New app generation was cancelled.",
                                appName,
                                3)))
                    .ConfigureAwait(false);
            }

            TemplateRenderResult renderResult;
            try
            {
                renderResult = renderer.Render(options, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return await WriteAsync(
                        output,
                        commandLine,
                        "atomui city new app",
                        CliEnvelope.FailedWithData(
                            "atomui city new app",
                            CliExitCodes.Failure,
                            new Dictionary<string, object?>
                            {
                                ["plan"] = plan,
                                ["artifacts"] = artifacts,
                            },
                            CliDiagnostic.Error(
                                "AUCCLI0106",
                                "New app generation was cancelled.",
                                appName,
                                3)))
                    .ConfigureAwait(false);
            }

            if (!renderResult.Succeeded)
            {
                var diagnostics = renderResult.Diagnostics
                    .Select(ToCliTemplateDiagnostic)
                    .ToArray();
                var exitCode = renderResult.Diagnostics.Any(static diagnostic => diagnostic.Code == "AUCTPL1004")
                    ? CliExitCodes.ArgumentError
                    : CliExitCodes.Failure;
                return await WriteAsync(
                        output,
                        commandLine,
                        "atomui city new app",
                        CliEnvelope.FailedWithData(
                            "atomui city new app",
                            exitCode,
                            new Dictionary<string, object?>
                            {
                                ["plan"] = plan,
                                ["artifacts"] = artifacts,
                            },
                            diagnostics))
                    .ConfigureAwait(false);
            }
        }

        return await WriteAsync(
                output,
                commandLine,
                "atomui city new app",
                CliEnvelope.Succeeded(
                    "atomui city new app",
                    new Dictionary<string, object?>
                    {
                        ["plan"] = plan,
                        ["artifacts"] = artifacts,
                    }))
            .ConfigureAwait(false);
    }

    private static async ValueTask<int> GenerateAsync(
        CliCommandLine commandLine,
        CliExecutionEnvironment environment,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var positionals = commandLine.Positionals;
        var requestedKind = positionals.Count > 2 ? positionals[2] : null;
        var name = positionals.Count > 3 ? positionals[3] : null;
        var command = requestedKind is null
            ? "atomui city generate"
            : $"atomui city generate {requestedKind}";

        if (requestedKind is null || string.IsNullOrWhiteSpace(name))
        {
            var argumentData = CreateUsageData();
            argumentData["kind"] = requestedKind;
            argumentData["name"] = name;
            return await WriteAsync(
                    output,
                    commandLine,
                    command,
                    CliEnvelope.FailedWithData(
                        command,
                        CliExitCodes.ArgumentError,
                        argumentData,
                        CliDiagnostic.Error(
                            "AUCCLI0501",
                            "Generation kind and name are required.",
                            name ?? requestedKind)))
                .ConfigureAwait(false);
        }

        if (!TryResolveGenerationKind(requestedKind, out var kind))
        {
            var message = requestedKind.Equals("plugin", StringComparison.OrdinalIgnoreCase)
                ? "Plugin generation is deferred with the PluginSystem release and is not available in this version."
                : $"Generation kind '{requestedKind}' is not supported.";
            var unsupportedData = CreateUsageData();
            unsupportedData["kind"] = requestedKind;
            unsupportedData["name"] = name;
            return await WriteAsync(
                    output,
                    commandLine,
                    command,
                    CliEnvelope.FailedWithData(
                        command,
                        CliExitCodes.ArgumentError,
                        unsupportedData,
                        CliDiagnostic.Error("AUCCLI0502", message, requestedKind, 2)))
                .ConfigureAwait(false);
        }

        string outputPath;
        try
        {
            outputPath = Path.GetFullPath(
                commandLine.GetOptionValue("--output") ?? environment.WorkingDirectory,
                environment.WorkingDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    command,
                    CliEnvelope.Failed(
                        command,
                        CliExitCodes.ArgumentError,
                        CliDiagnostic.Error("AUCTPL0001", "Output path is invalid.", commandLine.GetOptionValue("--output"))))
                .ConfigureAwait(false);
        }

        var projectResolution = ResolveGenerationProject(
            outputPath,
            commandLine.GetOptionValue("--project"));
        if (!projectResolution.Succeeded)
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    command,
                    CliEnvelope.FailedWithData(
                        command,
                        CliExitCodes.ArgumentError,
                        new Dictionary<string, object?>
                        {
                            ["workingDirectory"] = environment.WorkingDirectory,
                            ["outputPath"] = outputPath,
                            ["candidates"] = projectResolution.Candidates,
                        },
                        CliDiagnostic.Error(
                            "AUCCLI0503",
                            projectResolution.Message!,
                            commandLine.GetOptionValue("--project"))))
                .ConfigureAwait(false);
        }

        var rootNamespace = commandLine.GetOptionValue("--namespace") ??
            ReadRootNamespace(projectResolution.ProjectPath!) ??
            projectResolution.ProjectName!;
        var cultures = SplitOptionValues(commandLine.GetOptionValue("--culture"), ["en-US", "zh-CN"]);
        var dependencies = SplitOptionValues(commandLine.GetOptionValue("--depends-on"), []);
        var options = new GenerationTemplateOptions
        {
            Kind = kind,
            Name = name,
            ProjectName = projectResolution.ProjectName!,
            RootNamespace = rootNamespace,
            OutputPath = outputPath,
            RoutePath = commandLine.GetOptionValue("--route"),
            Cultures = cultures,
            ModuleDependencies = dependencies,
            IncludeTests = !commandLine.HasOption("--no-tests"),
            ReloadableConfiguration = commandLine.HasOption("--reloadable"),
        };

        var optionDiagnostics = options.Validate();
        if (optionDiagnostics.Count > 0)
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    command,
                    CliEnvelope.Failed(
                        command,
                        CliExitCodes.ArgumentError,
                        optionDiagnostics.Select(ToCliGenerationDiagnostic).ToArray()))
                .ConfigureAwait(false);
        }

        var renderer = new GenerationTemplateRenderer();
        var plan = renderer.CreatePlan(options);
        var artifacts = CreateArtifacts(plan);
        var data = new Dictionary<string, object?>
        {
            ["plan"] = plan,
            ["artifacts"] = artifacts,
            ["projectPath"] = projectResolution.ProjectPath,
            ["kind"] = kind.ToString(),
            ["name"] = name,
            ["operationId"] = plan.OperationId,
            ["dryRun"] = commandLine.HasOption("--dry-run"),
        };

        if (commandLine.HasOption("--dry-run"))
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    command,
                    CliEnvelope.Succeeded(command, data))
                .ConfigureAwait(false);
        }

        TemplateRenderResult renderResult;
        try
        {
            renderResult = renderer.Render(options, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    command,
                    CliEnvelope.FailedWithData(
                        command,
                        CliExitCodes.Failure,
                        data,
                        CliDiagnostic.Error("AUCCLI0504", "Generation was cancelled.", name, 3)))
                .ConfigureAwait(false);
        }

        if (!renderResult.Succeeded)
        {
            var exitCode = renderResult.Diagnostics.Any(static diagnostic =>
                diagnostic.Code is "AUCTPL1004" or "AUCTPL2001" or "AUCTPL2002" or "AUCTPL2003" or "AUCTPL2004" or "AUCTPL2005")
                ? CliExitCodes.ArgumentError
                : CliExitCodes.Failure;
            return await WriteAsync(
                    output,
                    commandLine,
                    command,
                    CliEnvelope.FailedWithData(
                        command,
                        exitCode,
                        data,
                        renderResult.Diagnostics.Select(ToCliGenerationDiagnostic).ToArray()))
                .ConfigureAwait(false);
        }

        data["changedFiles"] = renderResult.AppliedPaths;
        return await WriteAsync(
                output,
                commandLine,
                command,
                CliEnvelope.Succeeded(command, data))
            .ConfigureAwait(false);
    }

    private static bool TryResolveGenerationKind(
        string value,
        out GenerationTemplateKind kind)
    {
        switch (value.ToLowerInvariant())
        {
            case "module":
                kind = GenerationTemplateKind.Module;
                return true;
            case "page":
                kind = GenerationTemplateKind.Page;
                return true;
            case "test":
                kind = GenerationTemplateKind.Test;
                return true;
            case "config":
            case "configuration":
                kind = GenerationTemplateKind.Configuration;
                return true;
            case "localization":
                kind = GenerationTemplateKind.Localization;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static ProjectResolution ResolveGenerationProject(
        string outputPath,
        string? requestedProject)
    {
        try
        {
            var sourceRoot = Path.Combine(outputPath, "src");
            var candidates = Directory.Exists(sourceRoot)
                ? Directory
                    .EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories)
                    .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray()
                : [];
            var relativeCandidates = candidates
                .Select(path => Path.GetRelativePath(outputPath, path).Replace('\\', '/'))
                .ToArray();

            if (!string.IsNullOrWhiteSpace(requestedProject))
            {
                var matches = candidates
                    .Where(path =>
                        Path.GetFileNameWithoutExtension(path).Equals(requestedProject, StringComparison.OrdinalIgnoreCase) ||
                        Path.GetRelativePath(outputPath, path).Replace('\\', '/').Equals(requestedProject, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (matches.Length == 1)
                {
                    return IsCanonicalGenerationProject(outputPath, matches[0])
                        ? ProjectResolution.Success(matches[0], relativeCandidates)
                        : ProjectResolution.Failure(
                            "Generation requires the project layout src/<Project>/<Project>.csproj.",
                            relativeCandidates);
                }

                return ProjectResolution.Failure(
                    matches.Length == 0
                        ? $"Project '{requestedProject}' was not found under the workspace src directory."
                        : $"Project '{requestedProject}' is ambiguous under the workspace src directory.",
                    relativeCandidates);
            }

            return candidates.Length == 1
                ? IsCanonicalGenerationProject(outputPath, candidates[0])
                    ? ProjectResolution.Success(candidates[0], relativeCandidates)
                    : ProjectResolution.Failure(
                        "Generation requires the project layout src/<Project>/<Project>.csproj.",
                        relativeCandidates)
                : ProjectResolution.Failure(
                    candidates.Length == 0
                        ? "No project was found under the workspace src directory."
                        : "Multiple projects were found; specify one with --project.",
                    relativeCandidates);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return ProjectResolution.Failure(
                $"Project discovery failed: {exception.GetType().Name}.",
                []);
        }
    }

    private static bool IsCanonicalGenerationProject(string outputPath, string projectPath)
    {
        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var expectedPath = Path.GetFullPath(Path.Combine(
            outputPath,
            "src",
            projectName,
            projectName + ".csproj"));
        return string.Equals(
            Path.GetFullPath(projectPath),
            expectedPath,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string? ReadRootNamespace(string projectPath)
    {
        try
        {
            return XDocument
                .Load(projectPath)
                .Descendants("RootNamespace")
                .Select(static element => element.Value.Trim())
                .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> SplitOptionValues(
        string? value,
        IReadOnlyList<string> defaultValues)
    {
        if (value is null)
        {
            return Array.AsReadOnly(defaultValues.ToArray());
        }

        return Array.AsReadOnly(
            value
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToArray());
    }

    private static async ValueTask<int> DotnetCommandAsync(
        string command,
        CliCommandLine commandLine,
        CliExecutionEnvironment environment,
        TextWriter output,
        CancellationToken cancellationToken,
        Func<DotnetInvocation, CancellationToken, ValueTask<ProcessRunResult>> processRunner)
    {
        var invocation = DotnetInvocation.Create(command, commandLine, environment.WorkingDirectory, environment.IsCi);
        var cliCommand = "atomui city " + command;

        if (!Directory.Exists(environment.WorkingDirectory))
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    cliCommand,
                    CliEnvelope.FailedWithData(
                        cliCommand,
                        CliExitCodes.Failure,
                        new Dictionary<string, object?>
                        {
                            ["invocation"] = invocation,
                            ["workingDirectory"] = environment.WorkingDirectory,
                        },
                        CliDiagnostic.Error(
                            "AUCCLI0203",
                            $"Working directory does not exist: '{environment.WorkingDirectory}'.",
                            environment.WorkingDirectory)))
                .ConfigureAwait(false);
        }

        if (!commandLine.HasOption("--dry-run"))
        {
            try
            {
                var result = await processRunner(invocation, cancellationToken).ConfigureAwait(false);
                var data = CreateProcessData(invocation, result);
                return await WriteAsync(
                        output,
                        commandLine,
                        cliCommand,
                        result.ExitCode == 0
                            ? CliEnvelope.Succeeded(cliCommand, data)
                            : CliEnvelope.FailedWithData(
                                cliCommand,
                                result.ExitCode,
                                data,
                                CliDiagnostic.Error(
                                    "AUCCLI0201",
                                    CreateProcessFailureMessage(command, result),
                                    invocation.WorkingDirectory)))
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return await WriteAsync(
                        output,
                        commandLine,
                        cliCommand,
                        CliEnvelope.FailedWithData(
                            cliCommand,
                            CliExitCodes.Failure,
                            new Dictionary<string, object?>
                            {
                                ["invocation"] = invocation,
                            },
                            CliDiagnostic.Error(
                                "AUCCLI0202",
                                $"dotnet {command} was cancelled.",
                                invocation.WorkingDirectory)))
                    .ConfigureAwait(false);
            }
        }

        return await WriteAsync(
                output,
                commandLine,
                cliCommand,
                CliEnvelope.Succeeded(
                    cliCommand,
                    new Dictionary<string, object?> { ["invocation"] = invocation }))
            .ConfigureAwait(false);
    }

    private static Dictionary<string, object?> CreateProcessData(
        DotnetInvocation invocation,
        ProcessRunResult result)
    {
        return new Dictionary<string, object?>
        {
            ["invocation"] = invocation,
            ["exitCode"] = result.ExitCode,
            ["stdout"] = SummarizeProcessOutput(result.Output),
            ["stderr"] = SummarizeProcessOutput(result.Error),
            ["durationMs"] = result.DurationMs,
        };
    }

    private static string CreateProcessFailureMessage(string command, ProcessRunResult result)
    {
        var error = SummarizeProcessOutput(result.Error);
        return string.IsNullOrWhiteSpace(error)
            ? $"dotnet {command} exited with code {result.ExitCode}."
            : error;
    }

    private static string SummarizeProcessOutput(string value)
    {
        if (value.Length <= ProcessOutputSummaryLimit)
        {
            return value;
        }

        return value[..ProcessOutputSummaryLimit] + ProcessOutputTruncationSuffix;
    }

    private static async ValueTask<int> InspectAsync(
        CliCommandLine commandLine,
        CliExecutionEnvironment environment,
        TextWriter output)
    {
        var target = commandLine.Positionals.Count > 2 ? commandLine.Positionals[2] : "workspace";
        if (target != "workspace")
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    "atomui city inspect " + target,
                    CliEnvelope.Succeeded("atomui city inspect " + target, new Dictionary<string, object?> { ["target"] = target }))
                .ConfigureAwait(false);
        }

        var solutionPath = Path.Combine(environment.WorkingDirectory, "AtomUICity.slnx");
        var projects = File.Exists(solutionPath)
            ? ReadSolutionProjects(solutionPath, environment.WorkingDirectory)
            : [];
        var data = new Dictionary<string, object?>
        {
            ["solution"] = File.Exists(solutionPath) ? "AtomUICity.slnx" : null,
            ["projects"] = projects,
            ["docsStatus"] = new { exists = Directory.Exists(Path.Combine(environment.WorkingDirectory, "docs")) },
            ["testMatrixStatus"] = new { exists = Directory.Exists(Path.Combine(environment.WorkingDirectory, "tests")) },
            ["buildOutputStatus"] = new { path = "output", exists = Directory.Exists(Path.Combine(environment.WorkingDirectory, "output")) },
        };

        return await WriteAsync(
                output,
                commandLine,
                "atomui city inspect workspace",
                CliEnvelope.Succeeded("atomui city inspect workspace", data))
            .ConfigureAwait(false);
    }

    private static async ValueTask<int> PluginAsync(
        CliCommandLine commandLine,
        CliExecutionEnvironment environment,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var action = commandLine.Positionals.Count > 2 ? commandLine.Positionals[2] : "list";
        var pluginsRoot = Path.GetFullPath(
            commandLine.GetOptionValue("--plugins-root") ?? Path.Combine(environment.WorkingDirectory, "plugins"),
            environment.WorkingDirectory);

        if (action == "list")
        {
            var plugins = ReadInstalledPlugins(pluginsRoot);
            return await WriteAsync(
                    output,
                    commandLine,
                    "atomui city plugin list",
                    CliEnvelope.Succeeded("atomui city plugin list", new Dictionary<string, object?> { ["plugins"] = plugins }))
                .ConfigureAwait(false);
        }

        if (action == "install")
        {
            var packagePath = commandLine.Positionals.Count > 3 ? commandLine.Positionals[3] : string.Empty;
            var plan = new
            {
                schemaVersion = "1.0",
                operationId = "plugin-install",
                command = "atomui city plugin install",
                changes = new[]
                {
                    new { type = "install-plugin", path = packagePath, pluginsRoot },
                },
            };

            if (!commandLine.HasOption("--dry-run") && string.IsNullOrWhiteSpace(packagePath))
            {
                return await WriteAsync(
                        output,
                        commandLine,
                        "atomui city plugin install",
                        CliEnvelope.Failed(
                            "atomui city plugin install",
                            CliExitCodes.ArgumentError,
                            CliDiagnostic.Error("AUCCLI0301", "Plugin package path is required.")))
                    .ConfigureAwait(false);
            }

            if (!commandLine.HasOption("--dry-run") &&
                RequiresExplicitConfirmation(commandLine, environment))
            {
                return await WriteConfirmationRequiredAsync(
                        commandLine,
                        output,
                        "atomui city plugin install",
                        environment)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await WriteAsync(
                    output,
                    commandLine,
                    "atomui city plugin install",
                    CliEnvelope.Succeeded("atomui city plugin install", new Dictionary<string, object?> { ["plan"] = plan }))
                .ConfigureAwait(false);
        }

        if (action is "inspect" or "doctor")
        {
            return await PluginInspectOrDoctorAsync(action, commandLine, environment, output, cancellationToken).ConfigureAwait(false);
        }

        return await WriteAsync(
                output,
                commandLine,
                "atomui city plugin " + action,
                CliEnvelope.Succeeded(
                    "atomui city plugin " + action,
                    new Dictionary<string, object?> { ["action"] = action, ["pluginsRoot"] = pluginsRoot }))
            .ConfigureAwait(false);
    }

    private static async ValueTask<int> PluginInspectOrDoctorAsync(
        string action,
        CliCommandLine commandLine,
        CliExecutionEnvironment environment,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var command = "atomui city plugin " + action;
        var packageInput = commandLine.Positionals.Count > 3 ? commandLine.Positionals[3] : string.Empty;
        if (string.IsNullOrWhiteSpace(packageInput))
        {
            return await WriteAsync(
                    output,
                    commandLine,
                    command,
                    CliEnvelope.Failed(
                        command,
                        CliExitCodes.ArgumentError,
                        CliDiagnostic.Error("AUCCLI0302", "Plugin package path is required.")))
                .ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var packageRoot = ResolvePluginPackageRoot(packageInput, environment.WorkingDirectory);
        return action == "doctor"
            ? await PluginDoctorAsync(commandLine, output, command, packageRoot).ConfigureAwait(false)
            : await PluginInspectAsync(commandLine, output, command, packageRoot).ConfigureAwait(false);
    }

    private static async ValueTask<int> PluginInspectAsync(
        CliCommandLine commandLine,
        TextWriter output,
        string command,
        string packageRoot)
    {
        var manifestPath = ResolvePluginManifestPath(packageRoot);
        if (!File.Exists(manifestPath))
        {
            return await WritePluginValidationAsync(
                    commandLine,
                    output,
                    command,
                    packageRoot,
                    manifestPath,
                    null,
                    [
                        new PluginDiagnostic(
                            PluginDiagnosticIds.ManifestNotFound,
                            "Plugin package must contain atomui-city/plugin.json.",
                            Path: manifestPath),
                    ])
                .ConfigureAwait(false);
        }

        try
        {
            var manifest = PluginManifestReader.Read(manifestPath);
            var validation = PluginManifestValidator.Validate(manifest);
            return validation.Succeeded
                ? await WritePluginValidationSuccessAsync(commandLine, output, command, packageRoot, manifestPath, manifest).ConfigureAwait(false)
                : await WritePluginValidationAsync(commandLine, output, command, packageRoot, manifestPath, manifest, validation.Diagnostics).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            return await WritePluginValidationAsync(
                    commandLine,
                    output,
                    command,
                    packageRoot,
                    manifestPath,
                    null,
                    [
                        new PluginDiagnostic(
                            PluginDiagnosticIds.InvalidManifest,
                            exception.Message,
                            Path: manifestPath),
                    ])
                .ConfigureAwait(false);
        }
    }

    private static async ValueTask<int> PluginDoctorAsync(
        CliCommandLine commandLine,
        TextWriter output,
        string command,
        string packageRoot)
    {
        var manifestPath = PluginPackagePaths.GetManifestPath(packageRoot);
        PluginManifest? manifest = null;
        PluginValidationResult validation;

        try
        {
            validation = PluginPackageLayoutValidator.Validate(packageRoot);
            if (File.Exists(manifestPath))
            {
                manifest = PluginManifestReader.Read(manifestPath);
            }
        }
        catch (JsonException exception)
        {
            validation = new PluginValidationResult(
            [
                new PluginDiagnostic(
                    PluginDiagnosticIds.InvalidManifest,
                    exception.Message,
                    Path: manifestPath),
            ]);
        }

        return validation.Succeeded
            ? await WritePluginValidationSuccessAsync(commandLine, output, command, packageRoot, manifestPath, manifest).ConfigureAwait(false)
            : await WritePluginValidationAsync(commandLine, output, command, packageRoot, manifestPath, manifest, validation.Diagnostics).ConfigureAwait(false);
    }

    private static async ValueTask<int> WritePluginValidationSuccessAsync(
        CliCommandLine commandLine,
        TextWriter output,
        string command,
        string packageRoot,
        string manifestPath,
        PluginManifest? manifest)
    {
        return await WriteAsync(
                output,
                commandLine,
                command,
                CliEnvelope.Succeeded(
                    command,
                    CreatePluginValidationData(packageRoot, manifestPath, manifest, [])))
            .ConfigureAwait(false);
    }

    private static async ValueTask<int> WritePluginValidationAsync(
        CliCommandLine commandLine,
        TextWriter output,
        string command,
        string packageRoot,
        string manifestPath,
        PluginManifest? manifest,
        IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        return await WriteAsync(
                output,
                commandLine,
                command,
                CliEnvelope.FailedWithData(
                    command,
                    CliExitCodes.Failure,
                    CreatePluginValidationData(packageRoot, manifestPath, manifest, diagnostics),
                    diagnostics.Select(ToCliDiagnostic).ToArray()))
            .ConfigureAwait(false);
    }

    private static Dictionary<string, object?> CreatePluginValidationData(
        string packageRoot,
        string manifestPath,
        PluginManifest? manifest,
        IReadOnlyList<PluginDiagnostic> diagnostics)
    {
        return new Dictionary<string, object?>
        {
            ["packageRoot"] = packageRoot,
            ["manifestPath"] = manifestPath,
            ["manifest"] = manifest,
            ["succeeded"] = diagnostics.Count == 0,
            ["pluginDiagnostics"] = diagnostics,
        };
    }

    private static CliDiagnostic ToCliDiagnostic(PluginDiagnostic diagnostic)
    {
        return CliDiagnostic.Error(
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.Path ?? diagnostic.Field ?? diagnostic.PluginId);
    }

    private static CliDiagnostic ToCliTemplateDiagnostic(TemplateDiagnostic diagnostic)
    {
        var code = diagnostic.Code switch
        {
            "AUCTPL0001" when diagnostic.Context.TryGetValue("variable", out var variable) &&
                Equals(variable, "appName") => "AUCCLI0104",
            "AUCTPL0002" => "AUCCLI0102",
            "AUCTPL0301" => "AUCCLI0103",
            "AUCTPL1004" => "AUCCLI0105",
            _ => diagnostic.Code,
        };
        var target = diagnostic.Context.TryGetValue("path", out var path)
            ? path?.ToString()
            : diagnostic.Context.TryGetValue("rawValue", out var rawValue)
                ? rawValue?.ToString()
                : null;
        return CliDiagnostic.Error(code, diagnostic.Message, target);
    }

    private static CliDiagnostic ToCliGenerationDiagnostic(TemplateDiagnostic diagnostic)
    {
        var target = diagnostic.Context.TryGetValue("path", out var path)
            ? path?.ToString()
            : diagnostic.Context.TryGetValue("rawValue", out var rawValue)
                ? rawValue?.ToString()
                : null;
        return CliDiagnostic.Error(diagnostic.Code, diagnostic.Message, target);
    }

    private static string ResolvePluginPackageRoot(string packageInput, string workingDirectory)
    {
        var path = Path.GetFullPath(packageInput, workingDirectory);
        var directory = Path.GetDirectoryName(path);
        if (Path.GetFileName(path).Equals("plugin.json", StringComparison.Ordinal) &&
            directory is not null &&
            Path.GetFileName(directory).Equals("atomui-city", StringComparison.Ordinal))
        {
            return Directory.GetParent(directory)!.FullName;
        }

        return path;
    }

    private static string ResolvePluginManifestPath(string packageRoot)
    {
        return Path.GetFileName(packageRoot).Equals("plugin.json", StringComparison.Ordinal)
            ? packageRoot
            : PluginPackagePaths.GetManifestPath(packageRoot);
    }

    private static async ValueTask<int> GateCheckAsync(
        string gate,
        CliCommandLine commandLine,
        CliExecutionEnvironment environment,
        TextWriter output)
    {
        var path = Path.Combine(environment.WorkingDirectory, gate == "docs" ? "docs" : "tests");
        return await WriteAsync(
                output,
                commandLine,
                $"atomui city {gate} check",
                CliEnvelope.Succeeded(
                    $"atomui city {gate} check",
                    new Dictionary<string, object?> { ["path"] = path, ["exists"] = Directory.Exists(path) }))
            .ConfigureAwait(false);
    }

    private static async ValueTask<int> ExplainAsync(CliCommandLine commandLine, TextWriter output)
    {
        var code = commandLine.Positionals.Count > 2 ? commandLine.Positionals[2] : "AUCCLI0000";
        var data = new Dictionary<string, object?>
        {
            ["code"] = code,
            ["reason"] = "See AtomUI.City CLI diagnostics documentation.",
            ["suggestedAction"] = "Run the command with --json --pretty for structured diagnostics.",
        };

        return await WriteAsync(output, commandLine, "atomui city explain", CliEnvelope.Succeeded("atomui city explain", data)).ConfigureAwait(false);
    }

    private static async ValueTask<int> GenericPlanAsync(CliCommandLine commandLine, TextWriter output)
    {
        var command = "atomui " + string.Join(' ', commandLine.Positionals);
        var data = new Dictionary<string, object?>
        {
            ["plan"] = new
            {
                schemaVersion = "1.0",
                operationId = "cli-plan",
                command,
                changes = Array.Empty<object>(),
            },
        };

        return await WriteAsync(output, commandLine, command, CliEnvelope.Succeeded(command, data)).ConfigureAwait(false);
    }

    private static string BuildCommandName(CliCommandLine commandLine)
    {
        if (commandLine.Positionals.Count == 0 || commandLine.Positionals[0] != "city")
        {
            return "atomui city";
        }

        return commandLine.Positionals.Count == 1
            ? "atomui city"
            : "atomui city " + commandLine.Positionals[1];
    }

    private static Dictionary<string, object?> CreateUsageData()
    {
        return new Dictionary<string, object?>
        {
            ["usage"] = UsageLines,
        };
    }

    private static IReadOnlyList<object> CreateArtifacts(TemplatePlan plan)
    {
        return plan.Changes
            .Select(change => new
            {
                type = change.Type,
                path = change.Path,
            })
            .Cast<object>()
            .ToArray();
    }

    private static string ResolveTemplatePath(
        string outputPath,
        string relativePath)
    {
        return Path.Combine([outputPath, .. relativePath.Split('/')]);
    }

    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !IsIdentifierStart(value[0]))
        {
            return false;
        }

        return value.Skip(1).All(IsIdentifierPart);
    }

    private static bool IsIdentifierStart(char value)
    {
        return value == '_' || char.IsLetter(value);
    }

    private static bool IsIdentifierPart(char value)
    {
        return value == '_' || char.IsLetterOrDigit(value);
    }

    private static async ValueTask<int> ApplyAsync(
        CliCommandLine commandLine,
        CliExecutionEnvironment environment,
        TextWriter output)
    {
        var command = "atomui city apply";
        var planFile = commandLine.Positionals.Count > 2 ? commandLine.Positionals[2] : string.Empty;
        if (RequiresExplicitConfirmation(commandLine, environment))
        {
            return await WriteConfirmationRequiredAsync(commandLine, output, command, environment).ConfigureAwait(false);
        }

        var data = new Dictionary<string, object?> { ["planFile"] = planFile };

        return await WriteAsync(output, commandLine, command, CliEnvelope.Succeeded(command, data)).ConfigureAwait(false);
    }

    private static bool RequiresExplicitConfirmation(
        CliCommandLine commandLine,
        CliExecutionEnvironment environment)
    {
        return environment.IsNonInteractive && !commandLine.HasOption("--yes");
    }

    private static async ValueTask<int> WriteConfirmationRequiredAsync(
        CliCommandLine commandLine,
        TextWriter output,
        string command,
        CliExecutionEnvironment environment)
    {
        return await WriteAsync(
                output,
                commandLine,
                command,
                CliEnvelope.FailedWithData(
                    command,
                    CliExitCodes.ArgumentError,
                    new Dictionary<string, object?>
                    {
                        ["environment"] = CreateEnvironmentData(environment),
                        ["requiredOption"] = "--yes",
                    },
                    CliDiagnostic.Error(
                        "AUCCLI0401",
                        "Explicit --yes is required in non-interactive mode.",
                        "--yes")))
            .ConfigureAwait(false);
    }

    private static object CreateEnvironmentData(CliExecutionEnvironment environment)
    {
        return new
        {
            workingDirectory = environment.WorkingDirectory,
            ci = environment.IsCi,
            nonInteractive = environment.IsNonInteractive,
            stdinAvailable = environment.IsStdinAvailable,
        };
    }

    private static CliExecutionEnvironment CreateDefaultEnvironment()
    {
        var isCi = IsTruthyEnvironmentVariable("CI");
        var isNonInteractive = IsTruthyEnvironmentVariable("ATOMUI_CITY_NON_INTERACTIVE");
        return new CliExecutionEnvironment(
            Directory.GetCurrentDirectory(),
            isCi,
            isNonInteractive,
            isStdinAvailable: !Console.IsInputRedirected);
    }

    private static CliExecutionEnvironment CreateExecutionEnvironment(
        CliCommandLine commandLine,
        CliExecutionEnvironment baseEnvironment,
        string? workingDirectory)
    {
        var resolvedWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? baseEnvironment.WorkingDirectory
            : Path.GetFullPath(workingDirectory, baseEnvironment.WorkingDirectory);

        return new CliExecutionEnvironment(
            resolvedWorkingDirectory,
            baseEnvironment.IsCi || commandLine.HasOption("--ci"),
            baseEnvironment.IsNonInteractive || commandLine.HasOption("--non-interactive"),
            baseEnvironment.IsStdinAvailable);
    }

    private static bool IsTruthyEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return value is not null &&
            (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<object> ReadSolutionProjects(string solutionPath, string workingDirectory)
    {
        try
        {
            var document = XDocument.Load(solutionPath);
            return document
                .Descendants("Project")
                .Select(project => project.Attribute("Path")?.Value)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => new
                {
                    path,
                    exists = File.Exists(Path.Combine(workingDirectory, path!)),
                })
                .Cast<object>()
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<object> ReadInstalledPlugins(string pluginsRoot)
    {
        var installedRoot = Path.Combine(pluginsRoot, "installed");
        if (!Directory.Exists(installedRoot))
        {
            return [];
        }

        return Directory
            .EnumerateFiles(installedRoot, "plugin.json", SearchOption.AllDirectories)
            .Select(ReadPluginManifest)
            .Where(plugin => plugin is not null)
            .Cast<object>()
            .ToArray();
    }

    private static object? ReadPluginManifest(string manifestPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;

            return new
            {
                pluginId = root.GetProperty("pluginId").GetString(),
                packageId = root.GetProperty("packageId").GetString(),
                version = root.GetProperty("version").GetString(),
                manifestPath,
            };
        }
        catch
        {
            return null;
        }
    }

    private static async ValueTask<int> WriteAsync(
        TextWriter output,
        CliCommandLine commandLine,
        string command,
        CliEnvelope envelope)
    {
        if (commandLine.HasOption("--json"))
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = commandLine.HasOption("--pretty"),
            };

            await output.WriteLineAsync(JsonSerializer.Serialize(envelope, options)).ConfigureAwait(false);
        }
        else
        {
            await output.WriteLineAsync(envelope.Success ? $"{command}: OK" : $"{command}: failed").ConfigureAwait(false);
            if (!envelope.Success && TryGetUsage(envelope.Data, out var usage))
            {
                await output.WriteLineAsync("Usage:").ConfigureAwait(false);
                foreach (var line in usage)
                {
                    await output.WriteLineAsync("  " + line).ConfigureAwait(false);
                }
            }
        }

        return envelope.ExitCode;
    }

    private sealed record ProjectResolution(
        bool Succeeded,
        string? ProjectPath,
        string? ProjectName,
        string? Message,
        IReadOnlyList<string> Candidates)
    {
        public static ProjectResolution Success(
            string projectPath,
            IReadOnlyList<string> candidates)
        {
            return new ProjectResolution(
                true,
                projectPath,
                Path.GetFileNameWithoutExtension(projectPath),
                null,
                Array.AsReadOnly(candidates.ToArray()));
        }

        public static ProjectResolution Failure(
            string message,
            IReadOnlyList<string> candidates)
        {
            return new ProjectResolution(
                false,
                null,
                null,
                message,
                Array.AsReadOnly(candidates.ToArray()));
        }
    }

    private static bool TryGetUsage(
        object data,
        out IReadOnlyList<string> usage)
    {
        usage = [];
        if (data is not IReadOnlyDictionary<string, object?> dictionary ||
            !dictionary.TryGetValue("usage", out var value) ||
            value is not IEnumerable<object?> rawUsage)
        {
            return false;
        }

        usage = rawUsage
            .OfType<string>()
            .ToArray();

        return usage.Count > 0;
    }
}
