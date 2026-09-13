using System.Globalization;
using AtomUI.City.Localization;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal static class DogfoodLocalizationCatalog
{
    public const int KeyCount = 480;
    public const string ModuleId = "dogfood.module.shell";
    public const string RouteId = "dogfood.dashboard.overview";
    public const string WindowId = "main";
    public const string PluginId = "dogfood.synthetic.analytics";

    private static readonly (string Prefix, int Count, ResourceScope Scope, string? ScopeId)[] Groups =
    {
        ("Shell", 60, ResourceScope.Host, null),
        ("Navigation", 70, ResourceScope.Module, ModuleId),
        ("Commerce", 140, ResourceScope.Route, RouteId),
        ("Operations", 100, ResourceScope.Window, WindowId),
        ("SecurityData", 60, ResourceScope.Plugin, PluginId),
        ("Diagnostics", 50, ResourceScope.Presentation, null),
    };

    public static LocalizationLookupContext LookupContext { get; } =
        new(ModuleId, PluginId, RouteId, WindowId);

    public static void AddTo(LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.FallbackCultures.Add(CultureInfo.GetCultureInfo("en"));
        options.FallbackCultures.Add(CultureInfo.GetCultureInfo("zh-Hans"));

        foreach (var group in Groups)
        {
            options.LanguagePackages.Add(CreateDescriptor(group, "en-US"));
            options.LanguagePackages.Add(CreateDescriptor(group, "zh-CN"));
        }

        var distinctKeys = Groups.Sum(static group => group.Count);
        if (distinctKeys != KeyCount || options.LanguagePackages.Count != 12)
        {
            throw new InvalidOperationException("Localization catalog does not match the 480-key ledger.");
        }
    }

    private static LanguagePackageDescriptor CreateDescriptor(
        (string Prefix, int Count, ResourceScope Scope, string? ScopeId) group,
        string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        return new LanguagePackageDescriptor(
            $"Dogfood.{group.Prefix}",
            culture,
            group.Scope)
        {
            ScopeId = group.ScopeId,
            ProviderKind = LanguagePackageProviderKind.InMemory,
            FallbackCulture = CultureInfo.GetCultureInfo(
                cultureName == "zh-CN" ? "zh-Hans" : "en"),
            InMemoryResources = CreateResources(group.Prefix, group.Count, cultureName == "zh-CN"),
            CriticalResourceKeys = group.Prefix == "Shell"
                ? new[] { "Shell.000", "Shell.001", "Shell.002", "Shell.003", "Shell.004", "Shell.005" }
                : Array.Empty<string>(),
        };
    }

    private static IReadOnlyDictionary<string, string> CreateResources(
        string prefix,
        int count,
        bool chinese)
    {
        var resources = new Dictionary<string, string>(count, StringComparer.Ordinal);
        for (var index = 0; index < count; index++)
        {
            resources.Add($"{prefix}.{index:000}", $"{(chinese ? "中文文案" : "English copy")} {prefix} {index:000}");
        }

        if (prefix == "Shell")
        {
            var english = new[] { "Dashboard", "Commerce", "Fulfillment", "Customers", "Operations", "Administration" };
            var translated = new[] { "仪表盘", "商业运营", "履约", "客户", "运营中心", "系统管理" };
            for (var index = 0; index < english.Length; index++)
            {
                resources[$"Shell.{index:000}"] = chinese ? translated[index] : english[index];
            }

            resources["Shell.059"] = chinese ? "已处理 {0:N0} 个工作项" : "Processed {0:N0} work items";
        }

        return resources;
    }
}
