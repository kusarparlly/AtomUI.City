namespace CityLearning.Workbench.Configuration;

public sealed class WorkbenchOptions
{
    public WorkbenchOptions(string dataFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFilePath);
        DataFilePath = Path.GetFullPath(dataFilePath);
    }

    public string DataFilePath { get; }
}
