namespace AtomUI.City.Build.Tasks;

internal static class BuildTaskDiagnosticIds
{
    public const string InvalidOutputPath = "AUCBLD0001";
    public const string InvalidBuildProperty = "AUCBLD0002";
    public const string ManifestGenerationFailed = "AUCBLD0101";
    public const string ManifestValidationFailed = "AUCBLD0102";
    public const string InvalidPluginPackageLayout = "AUCBLD0201";
    public const string MultiplePluginMainAssemblies = "AUCBLD0202";
    public const string DynamicPluginNotAotCompatible = "AUCBLD0301";
    public const string InvalidApplicationPublishLayout = "AUCBLD0401";
}
