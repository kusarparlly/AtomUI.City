using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodApiCoverageWorkload
{
    private static readonly HashSet<string> AllowedClassifications =
    [
        "UI", "RT", "NEG", "BUILD", "MODEL", "INDIRECT", "BLOCKED", "RETIRED",
    ];

    private readonly DogfoodRunOptions _options;
    private readonly DogfoodRunLedger _ledger;
    private int _executed;

    public DogfoodApiCoverageWorkload(DogfoodRunOptions options, DogfoodRunLedger ledger)
    {
        _options = options;
        _ledger = ledger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _executed, 1) != 0)
        {
            throw new InvalidOperationException("The API coverage workload can only run once.");
        }

        var policyPath = Path.Combine(AppContext.BaseDirectory, "api-coverage.json");
        var policy = JsonSerializer.Deserialize<DogfoodApiCoveragePolicy>(
            await File.ReadAllTextAsync(policyPath, cancellationToken).ConfigureAwait(false),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The Dogfood API coverage policy is empty.");
        var results = new List<DogfoodApiModuleResult>();
        var unclassified = 0;
        var uncovered = 0;
        foreach (var module in policy.Modules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!AllowedClassifications.Contains(module.Classification))
            {
                unclassified++;
                continue;
            }

            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .SingleOrDefault(candidate =>
                    string.Equals(candidate.GetName().Name, module.Assembly, StringComparison.Ordinal))
                ?? Assembly.Load(module.Assembly);
            var members = BuildInventory(assembly);
            var hash = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(string.Join('\n', members))));
            var drifted = !string.IsNullOrWhiteSpace(module.ExpectedSha256) &&
                !string.Equals(hash, module.ExpectedSha256, StringComparison.OrdinalIgnoreCase);
            var covered = _ledger.Count(module.EvidenceCategory) > 0;
            if (!covered)
            {
                uncovered++;
            }

            if (drifted)
            {
                unclassified++;
            }

            results.Add(new DogfoodApiModuleResult(
                module.Module,
                module.Assembly,
                module.Classification,
                module.EvidenceCategory,
                members.Count,
                hash,
                module.ExpectedSha256,
                covered,
                drifted,
                members));
            _ledger.Record("api-module", module.Module);
        }

        var report = new DogfoodApiCoverageReport(
            1,
            results.Sum(static result => result.PublicMemberCount),
            unclassified,
            uncovered,
            results);
        var path = Path.Combine(_options.ArtifactRoot, "api-coverage.json");
        Directory.CreateDirectory(_options.ArtifactRoot);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(
                report,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                }),
            cancellationToken).ConfigureAwait(false);
        Console.WriteLine(
            $"DESKTOP_DOGFOOD_API members={report.PublicMemberCount} unclassified={unclassified} " +
            $"requiredUncovered={uncovered} path={path}");
        if (unclassified != 0 || uncovered != 0)
        {
            throw new InvalidOperationException(
                $"Dogfood API gate failed: unclassified={unclassified}, required-uncovered={uncovered}.");
        }
    }

    private static IReadOnlyList<string> BuildInventory(Assembly assembly)
    {
        var inventory = new List<string>();
        foreach (var type in assembly.ExportedTypes.OrderBy(static type => type.FullName, StringComparer.Ordinal))
        {
            var typeId = FormatType(type);
            inventory.Add($"T:{typeId}");
            foreach (var constructor in type.GetConstructors(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                inventory.Add($"C:{typeId}({FormatParameters(constructor.GetParameters())})");
            }

            foreach (var method in type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName)
                {
                    continue;
                }

                inventory.Add(
                    $"M:{typeId}.{method.Name}`{method.GetGenericArguments().Length}" +
                    $"({FormatParameters(method.GetParameters())})->{FormatType(method.ReturnType)}");
            }

            foreach (var property in type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                inventory.Add($"P:{typeId}.{property.Name}:{FormatType(property.PropertyType)}");
            }

            foreach (var @event in type.GetEvents(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                inventory.Add($"E:{typeId}.{@event.Name}:{FormatType(@event.EventHandlerType!)}");
            }

            foreach (var field in type.GetFields(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!field.IsSpecialName)
                {
                    inventory.Add($"F:{typeId}.{field.Name}:{FormatType(field.FieldType)}");
                }
            }
        }

        inventory.Sort(StringComparer.Ordinal);
        return inventory;
    }

    private static string FormatParameters(IEnumerable<ParameterInfo> parameters) =>
        string.Join(',', parameters.Select(parameter => FormatType(parameter.ParameterType)));

    private static string FormatType(Type type)
    {
        if (type.IsByRef)
        {
            return FormatType(type.GetElementType()!) + "&";
        }

        if (type.IsArray)
        {
            return FormatType(type.GetElementType()!) + "[]";
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var name = (type.GetGenericTypeDefinition().FullName ?? type.Name).Split('`')[0];
        return $"{name}<{string.Join(',', type.GetGenericArguments().Select(FormatType))}>";
    }
}

internal sealed record DogfoodApiCoveragePolicy(
    int SchemaVersion,
    IReadOnlyList<DogfoodApiModulePolicy> Modules);

internal sealed record DogfoodApiModulePolicy(
    string Module,
    string Assembly,
    string Classification,
    string EvidenceCategory,
    string ExpectedSha256);

internal sealed record DogfoodApiCoverageReport(
    int SchemaVersion,
    int PublicMemberCount,
    int Unclassified,
    int RequiredUncovered,
    IReadOnlyList<DogfoodApiModuleResult> Modules);

internal sealed record DogfoodApiModuleResult(
    string Module,
    string Assembly,
    string Classification,
    string EvidenceCategory,
    int PublicMemberCount,
    string ObservedSha256,
    string ExpectedSha256,
    bool Covered,
    bool Drifted,
    IReadOnlyList<string> Members);
