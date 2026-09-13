using System.Collections.Concurrent;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal static class ModuleRuntimeLedger
{
    private static readonly ConcurrentDictionary<string, ModuleCounters> Counters =
        new(StringComparer.Ordinal);

    public static void Constructed(string module) => Update(module, static value => value with
    {
        Constructed = value.Constructed + 1,
    });

    public static void Configured(string module) => Update(module, static value => value with
    {
        Configured = value.Configured + 1,
    });

    public static void Initialized(string module) => Update(module, static value => value with
    {
        Initialized = value.Initialized + 1,
    });

    public static void Stopped(string module) => Update(module, static value => value with
    {
        Stopped = value.Stopped + 1,
    });

    public static void VerifyStarted()
    {
        Verify(expectedStopped: 0);
    }

    public static void VerifyStopped()
    {
        Verify(expectedStopped: 1);
    }

    private static void Verify(int expectedStopped)
    {
        var expected = DogfoodModuleCatalog.Names;
        if (expected.Count != 48 || Counters.Count != expected.Count)
        {
            var missing = expected.Where(name => !Counters.ContainsKey(name));
            throw new InvalidOperationException(
                $"Expected 48 application modules, catalog={expected.Count}, observed={Counters.Count}, " +
                $"missing=[{string.Join(", ", missing)}].");
        }

        foreach (var name in expected)
        {
            if (!Counters.TryGetValue(name, out var counters) ||
                counters.Constructed != 1 ||
                counters.Configured != 1 ||
                counters.Initialized != 1 ||
                counters.Stopped != expectedStopped)
            {
                throw new InvalidOperationException(
                    $"Module '{name}' lifecycle mismatch: {counters}; expected stopped={expectedStopped}.");
            }
        }
    }

    private static void Update(string module, Func<ModuleCounters, ModuleCounters> update)
    {
        Counters.AddOrUpdate(module, _ => update(default), (_, current) => update(current));
    }

    private readonly record struct ModuleCounters(
        int Constructed,
        int Configured,
        int Initialized,
        int Stopped);
}
