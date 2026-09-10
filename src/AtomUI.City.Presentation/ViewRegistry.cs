using AtomUI.City.Core.Diagnostics;

namespace AtomUI.City.Presentation;

public sealed class ViewRegistry : IViewRegistry
{
    private readonly Dictionary<ViewRegistrationKey, List<ViewDescriptor>> _descriptorLayers = new();
    private readonly ReaderWriterLockSlim _gate = new();
    private readonly IHostDiagnostics? _diagnostics;

    public ViewRegistry()
    {
    }

    public ViewRegistry(IHostDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        _diagnostics = diagnostics;
    }

    public void Register(ViewDescriptor descriptor)
    {
        Register(descriptor, options: null);
    }

    public void Register(
        ViewDescriptor descriptor,
        ViewRegistrationOptions? options)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var key = ViewRegistrationKey.Create(descriptor.ViewModelType, descriptor.ViewKey);

        _gate.EnterWriteLock();
        try
        {
            RegisterCore(key, descriptor, options);
        }
        finally
        {
            _gate.ExitWriteLock();
        }
    }

    public void RegisterManifest(IEnumerable<ViewDescriptor> descriptors)
    {
        RegisterManifest(descriptors, options: null);
    }

    public void RegisterManifest(
        IEnumerable<ViewDescriptor> descriptors,
        ViewRegistrationOptions? options)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var entries = descriptors
            .Select(
                descriptor =>
                {
                    ArgumentNullException.ThrowIfNull(descriptor);

                    return new ViewRegistrationEntry(
                        ViewRegistrationKey.Create(descriptor.ViewModelType, descriptor.ViewKey),
                        descriptor);
                })
            .ToArray();

        var duplicate = entries
            .GroupBy(static entry => entry.Key)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw CreateDuplicateException(duplicate.First().Descriptor, duplicate.Key);
        }

        _gate.EnterWriteLock();
        try
        {
            if (options?.ReplaceExisting != true)
            {
                foreach (var entry in entries)
                {
                    if (_descriptorLayers.ContainsKey(entry.Key))
                    {
                        throw CreateDuplicateException(entry.Descriptor, entry.Key);
                    }
                }
            }

            foreach (var entry in entries)
            {
                RegisterCore(entry.Key, entry.Descriptor, options);
            }
        }
        finally
        {
            _gate.ExitWriteLock();
        }
    }

    public int RevokePlugin(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        return Revoke(descriptor => string.Equals(descriptor.PluginId, pluginId, StringComparison.Ordinal));
    }

    public int RevokeContribution(string contributionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);

        return Revoke(descriptor => string.Equals(descriptor.ContributionId, contributionId, StringComparison.Ordinal));
    }

    private int Revoke(Func<ViewDescriptor, bool> predicate)
    {
        _gate.EnterWriteLock();
        try
        {
            var revokedCount = 0;
            foreach (var (key, layers) in _descriptorLayers.ToArray())
            {
                revokedCount += layers.RemoveAll(descriptor => predicate(descriptor));
                if (layers.Count == 0)
                {
                    _descriptorLayers.Remove(key);
                }
            }

            return revokedCount;
        }
        finally
        {
            _gate.ExitWriteLock();
        }
    }

    public bool TryLocate(Type viewModelType, out ViewDescriptor? descriptor)
    {
        return TryLocate(viewModelType, viewKey: null, out descriptor);
    }

    public bool TryLocate(
        Type viewModelType,
        string? viewKey,
        out ViewDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);

        return TryLocate(
            new ViewLookupRequest(viewModelType, viewKey),
            out descriptor);
    }

    public bool TryLocate(
        ViewLookupRequest request,
        out ViewDescriptor? descriptor)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool located;
        _gate.EnterReadLock();
        try
        {
            located = _descriptorLayers.TryGetValue(
                ViewRegistrationKey.Create(request.ViewModelType, request.ViewKey),
                out var layers);
            descriptor = located ? layers![^1] : null;
        }
        finally
        {
            _gate.ExitReadLock();
        }

        if (located && descriptor is not null)
        {
            WriteMatchedDiagnostic(request, descriptor);
        }
        else
        {
            WriteFailedDiagnostic(request);
        }

        return located;
    }

    public ViewDescriptor Locate(Type viewModelType, string? viewKey = null)
    {
        return Locate(new ViewLookupRequest(viewModelType, viewKey));
    }

    public ViewDescriptor Locate(ViewLookupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (TryLocate(request, out var descriptor) && descriptor is not null)
        {
            return descriptor;
        }

        throw new PresentationException(
            PresentationError.ViewNotFound,
            $"No view was registered for view model '{request.ViewModelType.FullName}'.");
    }

    private void RegisterCore(
        ViewRegistrationKey key,
        ViewDescriptor descriptor,
        ViewRegistrationOptions? options)
    {
        if (!_descriptorLayers.TryGetValue(key, out var layers))
        {
            layers = [];
            _descriptorLayers[key] = layers;
        }

        if (layers.Count != 0 && options?.ReplaceExisting != true)
        {
            throw CreateDuplicateException(descriptor, key);
        }

        layers.Add(descriptor);
    }

    private static PresentationException CreateDuplicateException(
        ViewDescriptor descriptor,
        ViewRegistrationKey key)
    {
        return new PresentationException(
            PresentationError.DuplicateView,
            $"View model '{descriptor.ViewModelType.FullName}' already has a view registered for key '{key.ViewKey}'.");
    }

    private readonly record struct ViewRegistrationKey(Type ViewModelType, string ViewKey)
    {
        public static ViewRegistrationKey Create(Type viewModelType, string? viewKey)
        {
            return new ViewRegistrationKey(
                viewModelType,
                string.IsNullOrWhiteSpace(viewKey) ? string.Empty : viewKey);
        }
    }

    private void WriteMatchedDiagnostic(
        ViewLookupRequest request,
        ViewDescriptor descriptor)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.ViewLocatorMatched,
            $"View locator matched view model '{request.ViewModelType.FullName}' to view '{descriptor.ViewType.FullName}' with key '{NormalizeViewKey(request.ViewKey)}'.",
            HostDiagnosticSeverity.Info)
        {
            Context = CreateDiagnosticContext(request, descriptor),
        });
    }

    private void WriteFailedDiagnostic(ViewLookupRequest request)
    {
        _diagnostics?.Write(new HostDiagnosticRecord(
            PresentationDiagnosticIds.ViewLocatorFailed,
            $"View locator failed for view model '{request.ViewModelType.FullName}' with key '{NormalizeViewKey(request.ViewKey)}'.",
            HostDiagnosticSeverity.Warning)
        {
            Context = CreateDiagnosticContext(request, descriptor: null),
        });
    }

    private static IReadOnlyDictionary<string, string?> CreateDiagnosticContext(
        ViewLookupRequest request,
        ViewDescriptor? descriptor)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["viewModelType"] = request.ViewModelType.FullName,
            ["viewType"] = descriptor?.ViewType.FullName,
            ["viewKey"] = NormalizeViewKey(request.ViewKey),
            ["routeId"] = request.RouteId,
            ["ownerId"] = request.OwnerId,
            ["pluginId"] = descriptor?.PluginId,
            ["contributionId"] = descriptor?.ContributionId,
        };
    }

    private static string NormalizeViewKey(string? viewKey)
    {
        return string.IsNullOrWhiteSpace(viewKey) ? "<default>" : viewKey;
    }

    private readonly record struct ViewRegistrationEntry(
        ViewRegistrationKey Key,
        ViewDescriptor Descriptor);
}
