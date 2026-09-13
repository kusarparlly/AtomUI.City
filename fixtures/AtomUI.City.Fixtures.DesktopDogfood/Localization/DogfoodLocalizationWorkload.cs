using AtomUI.City.Localization;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodLocalizationWorkload
{
    private readonly ILocalizationService _localization;

    public DogfoodLocalizationWorkload(ILocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public async Task InitializeAsync(
        Action<IReadOnlyDictionary<string, string>> applyNavigation,
        Action<string> applyStatus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(applyNavigation);
        ArgumentNullException.ThrowIfNull(applyStatus);

        using var scope = _localization.ActivateScope(DogfoodLocalizationCatalog.LookupContext);
        var tracked = new List<ILocalizedText>();
        var notifications = 0;
        try
        {
            for (var index = 0; index < 16; index++)
            {
                var text = await _localization.CreateTextAsync(
                    $"Shell.{index:000}",
                    DogfoodLocalizationCatalog.LookupContext,
                    cancellationToken);
                text.Changed += (_, _) => Interlocked.Increment(ref notifications);
                tracked.Add(text);
            }

            for (var iteration = 0; iteration < 100; iteration++)
            {
                var culture = iteration % 2 == 0 ? "zh-CN" : "en-US";
                var result = await _localization.SetCultureAsync(culture, cancellationToken);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(result.Error?.Message ?? "Culture switch failed.");
                }
            }

            var lookups = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < DogfoodLocalizationCatalog.KeyCount; index++)
            {
                var (prefix, localIndex) = ResolveKey(index);
                var localized = await _localization.GetStringAsync(
                    $"{prefix}.{localIndex:000}",
                    DogfoodLocalizationCatalog.LookupContext,
                    cancellationToken);
                if (localized.IsMissing || localized.Culture.Name != "en-US")
                {
                    throw new InvalidOperationException($"Localization key '{localized.Key}' failed its final lookup.");
                }

                lookups[localized.Key] = localized.Value;
            }

            var message = await _localization.GetMessageAsync(
                "Shell.059",
                new object?[] { 48_000 },
                DogfoodLocalizationCatalog.LookupContext,
                cancellationToken);
            if (message.IsMissing || message.IsFormatFailed || !message.Value.Contains("48,000", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Localized formatted message did not preserve invariant workload data.");
            }

            applyNavigation(Enumerable.Range(0, 6).ToDictionary(
                index => new[] { "Dashboard", "Commerce", "Fulfillment", "Customers", "Operations", "Administration" }[index],
                index => lookups[$"Shell.{index:000}"],
                StringComparer.Ordinal));
            applyStatus($"{message.Value}; culture revision {_localization.CultureRevision}");

            if (notifications < 16 * 99)
            {
                throw new InvalidOperationException(
                    $"Tracked localization texts expected at least {16 * 99} refresh notifications; observed {notifications}.");
            }

            Console.WriteLine(
                $"DESKTOP_DOGFOOD_LOCALIZATION keys=480 packages=12 switches=100 tracked=16 notifications={notifications} culture={_localization.CurrentCulture.Name}");
        }
        finally
        {
            foreach (var text in tracked)
            {
                text.Dispose();
            }
        }
    }

    private static (string Prefix, int LocalIndex) ResolveKey(int index)
    {
        return index switch
        {
            < 60 => ("Shell", index),
            < 130 => ("Navigation", index - 60),
            < 270 => ("Commerce", index - 130),
            < 370 => ("Operations", index - 270),
            < 430 => ("SecurityData", index - 370),
            _ => ("Diagnostics", index - 430),
        };
    }
}
