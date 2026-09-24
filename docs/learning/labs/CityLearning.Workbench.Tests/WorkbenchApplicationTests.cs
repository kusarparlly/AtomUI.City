using System.Diagnostics;
using CityLearning.Workbench.Services;

namespace CityLearning.Workbench.Tests;

public sealed class WorkbenchApplicationTests
{
    [Fact]
    public async Task ApplicationProcess_UsesGeneratedServices_AndPersistsWorkItems()
    {
        var testDirectory = Path.Combine(Path.GetTempPath(), "city-learning", Guid.NewGuid().ToString("N"));
        var dataFilePath = Path.Combine(testDirectory, "work-items.json");

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            startInfo.ArgumentList.Add(typeof(App).Assembly.Location);
            startInfo.ArgumentList.Add("--verify-host");
            startInfo.Environment["CITY_LEARNING_DATA_FILE"] = dataFilePath;

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start the Workbench process.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await process.WaitForExitAsync(timeout.Token);
            var standardError = await process.StandardError.ReadToEndAsync(timeout.Token);

            Assert.True(
                process.ExitCode == 0,
                $"Workbench exited with {process.ExitCode}: {standardError}");
            Assert.True(File.Exists(dataFilePath));
            Assert.Contains("City Host verification", await File.ReadAllTextAsync(dataFilePath));
        }
        finally
        {
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task WorkItemService_RejectsBlankTitles()
    {
        var repository = new InMemoryWorkItemRepository();
        using var service = new WorkItemService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() => service.AddAsync("  "));
        Assert.Empty(await service.GetAllAsync());
    }
}
