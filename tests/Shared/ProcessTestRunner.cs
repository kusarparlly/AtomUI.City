using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AtomUI.City.Testing.Processes;

internal sealed record TestProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal static class ProcessTestRunner
{
    private const uint SemNoGpFaultErrorBox = 0x0002;
    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(2);
    private static readonly object ProcessStartLock = new();

    public static async Task<TestProcessResult> RunAsync(
        string fileName,
        string? workingDirectory,
        TimeSpan timeout,
        params string[] arguments)
    {
        return await RunAsync(
                fileName,
                workingDirectory,
                timeout,
                environment: null,
                arguments)
            .ConfigureAwait(false);
    }

    public static async Task<TestProcessResult> RunAsync(
        string fileName,
        string? workingDirectory,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string?>? environment,
        params string[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory ?? string.Empty,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            ErrorDialog = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        startInfo.Environment["DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER"] = "1";
        startInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        if (environment is not null)
        {
            foreach (var entry in environment)
            {
                if (entry.Value is null)
                {
                    startInfo.Environment.Remove(entry.Key);
                }
                else
                {
                    startInfo.Environment[entry.Key] = entry.Value;
                }
            }
        }

        using var process = new Process { StartInfo = startInfo };
        StartWithoutWindowsErrorDialog(process);

        using var outputCaptureSource = new CancellationTokenSource();
        var standardOutputTask = CaptureAsync(process.StandardOutput, outputCaptureSource.Token);
        var standardErrorTask = CaptureAsync(process.StandardError, outputCaptureSource.Token);
        using var timeoutSource = new CancellationTokenSource(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
            outputCaptureSource.Cancel();
            throw new TimeoutException(
                $"Process '{fileName}' did not exit within {timeout}.");
        }

        var outputTasks = Task.WhenAll(standardOutputTask, standardErrorTask);
        if (await Task.WhenAny(outputTasks, Task.Delay(OutputDrainTimeout)).ConfigureAwait(false) != outputTasks)
        {
            outputCaptureSource.Cancel();
        }

        return new TestProcessResult(
            process.ExitCode,
            await standardOutputTask.ConfigureAwait(false),
            await standardErrorTask.ConfigureAwait(false));
    }

    private static async Task<string> CaptureAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var output = new StringBuilder();

        try
        {
            while (true)
            {
                var count = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }
                output.Append(buffer, 0, count);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A compiler server can retain an inherited pipe after its root process exits.
            // Return all output already observed instead of waiting indefinitely for EOF.
        }

        return output.ToString();
    }

    private static void StartWithoutWindowsErrorDialog(Process process)
    {
        if (!OperatingSystem.IsWindows())
        {
            process.Start();
            return;
        }

        lock (ProcessStartLock)
        {
            var previousMode = GetErrorMode();
            try
            {
                SetErrorMode(previousMode | SemNoGpFaultErrorBox);
                process.Start();
            }
            finally
            {
                SetErrorMode(previousMode);
            }
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetErrorMode();

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint errorMode);
}
