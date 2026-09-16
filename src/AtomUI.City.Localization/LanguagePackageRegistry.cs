namespace AtomUI.City.Localization;

/// <summary>
/// Represents language package registry.
/// </summary>
public sealed class LanguagePackageRegistry
{
    private readonly Dictionary<(string CultureName, string PackageId), LanguagePackageRegistration> _registrations = [];
    private readonly HashSet<string> _revokedOwners =
        new(StringComparer.Ordinal);
    private readonly object _syncRoot = new();

    internal event Action<IReadOnlyList<LanguagePackageDescriptor>>? DescriptorsRevoked;

    /// <summary>
    /// Represents the registrations value.
    /// </summary>
    public IReadOnlyList<LanguagePackageRegistration> Registrations
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_registrations.Values.ToArray());
            }
        }
    }

    /// <summary>
    /// Represents the descriptors value.
    /// </summary>
    public IReadOnlyList<LanguagePackageDescriptor> Descriptors
    {
        get
        {
            lock (_syncRoot)
            {
                return Array.AsReadOnly(_registrations.Values
                    .Select(registration => registration.Descriptor)
                    .ToArray());
            }
        }
    }

    /// <summary>
    /// Executes the register operation.
    /// </summary>
    public LocalizationResult Register(
        LanguagePackageDescriptor descriptor,
        string ownerId)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return RegisterRange([descriptor], ownerId);
    }

    /// <summary>
    /// Executes the register range operation.
    /// </summary>
    public LocalizationResult RegisterRange(
        IEnumerable<LanguagePackageDescriptor> descriptors,
        string ownerId)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var descriptorArray = descriptors.ToArray();
        if (descriptorArray.Any(descriptor => descriptor is null))
        {
            throw new ArgumentException(
                "Language package descriptors cannot contain null values.",
                nameof(descriptors));
        }

        foreach (var descriptor in descriptorArray)
        {
            var validationResult = ValidateDescriptor(descriptor);
            if (!validationResult.Succeeded)
            {
                return validationResult;
            }
        }

        lock (_syncRoot)
        {
            if (_revokedOwners.Contains(ownerId))
            {
                return LocalizationResult.Failed(
                    new LocalizationError(
                        LocalizationErrorKind.OwnerRevoked,
                        $"Language package owner '{ownerId}' has been revoked."));
            }

            var pendingKeys = new HashSet<(string CultureName, string PackageId)>();
            foreach (var descriptor in descriptorArray)
            {
                var key = CreateKey(descriptor);
                if (_registrations.ContainsKey(key) || !pendingKeys.Add(key))
                {
                    return LocalizationResult.Failed(
                        new LocalizationError(
                            LocalizationErrorKind.PackageAlreadyRegistered,
                            $"Language package '{descriptor.PackageId}' for culture " +
                            $"'{descriptor.Culture.Name}' is already registered."));
                }
            }

            foreach (var descriptor in descriptorArray)
            {
                _registrations.Add(
                    CreateKey(descriptor),
                    new LanguagePackageRegistration(descriptor, ownerId));
            }

            return LocalizationResult.Success();
        }
    }

    /// <summary>
    /// Executes the revoke owner operation.
    /// </summary>
    public int RevokeOwner(string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        LanguagePackageDescriptor[] revokedDescriptors;
        lock (_syncRoot)
        {
            _revokedOwners.Add(ownerId);
            var revokedKeys = _registrations
                .Where(pair => string.Equals(pair.Value.OwnerId, ownerId, StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .ToArray();

            revokedDescriptors = revokedKeys
                .Select(key => _registrations[key].Descriptor)
                .ToArray();

            foreach (var key in revokedKeys)
            {
                _registrations.Remove(key);
            }
        }

        NotifyDescriptorsRevoked(revokedDescriptors);

        return revokedDescriptors.Length;
    }

    internal static LanguagePackageRegistry CreateWithHostDescriptors(
        IEnumerable<LanguagePackageDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);

        var registry = new LanguagePackageRegistry();
        foreach (var descriptor in descriptors)
        {
            var result = registry.Register(descriptor, "host");
            if (!result.Succeeded)
            {
                throw new ArgumentException(result.Error!.Message, nameof(descriptors));
            }
        }

        return registry;
    }

    internal IReadOnlyList<LanguagePackageDescriptor> RevokeContribution(string contributionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionId);

        LanguagePackageDescriptor[] revokedDescriptors;
        lock (_syncRoot)
        {
            var revokedKeys = _registrations
                .Where(pair => string.Equals(
                    pair.Value.Descriptor.ContributionId,
                    contributionId,
                    StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .ToArray();

            revokedDescriptors = revokedKeys
                .Select(key => _registrations[key].Descriptor)
                .ToArray();

            foreach (var key in revokedKeys)
            {
                _registrations.Remove(key);
            }
        }

        NotifyDescriptorsRevoked(revokedDescriptors);

        return Array.AsReadOnly(revokedDescriptors);
    }

    internal bool Contains(LanguagePackageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        lock (_syncRoot)
        {
            return _registrations.TryGetValue(CreateKey(descriptor), out var registration)
                && ReferenceEquals(registration.Descriptor, descriptor);
        }
    }

    private void NotifyDescriptorsRevoked(IReadOnlyList<LanguagePackageDescriptor> descriptors)
    {
        if (descriptors.Count > 0)
        {
            DescriptorsRevoked?.Invoke(descriptors);
        }
    }

    private static (string CultureName, string PackageId) CreateKey(LanguagePackageDescriptor descriptor)
    {
        return (descriptor.Culture.Name, descriptor.PackageId);
    }

    private static bool RequiresScopeId(ResourceScope scope)
    {
        return scope is ResourceScope.Module
            or ResourceScope.Plugin
            or ResourceScope.Route
            or ResourceScope.Window;
    }

    private static LocalizationResult ValidateDescriptor(LanguagePackageDescriptor descriptor)
    {
        if (!Enum.IsDefined(descriptor.ProviderKind))
        {
            return LocalizationResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.InvalidDescriptor,
                    $"Language package '{descriptor.PackageId}' has an unknown provider kind."));
        }

        if (RequiresScopeId(descriptor.Scope) && string.IsNullOrWhiteSpace(descriptor.ScopeId))
        {
            return LocalizationResult.Failed(
                new LocalizationError(
                    LocalizationErrorKind.InvalidDescriptor,
                    $"Language package '{descriptor.PackageId}' in scope '{descriptor.Scope}' requires a scope id."));
        }

        return LocalizationResult.Success();
    }
}
