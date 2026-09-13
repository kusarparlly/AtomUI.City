namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodSecurityWorkspace : IDisposable
{
    public DogfoodSecurityWorkspace()
    {
        RootPath = Path.Combine(
            Path.GetTempPath(),
            "AtomUI.City.DesktopDogfood",
            $"{Environment.ProcessId}-{Guid.NewGuid():N}");
    }

    public string RootPath { get; }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
