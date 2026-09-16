namespace AtomUI.City.Localization;

/// <summary>
/// Represents localization diagnostic record.
/// </summary>
/// <param name="Code">The code value.</param>
/// <param name="Message">The message value.</param>
/// <param name="Severity">The severity value.</param>
/// <param name="CultureName">The culture name value.</param>
/// <param name="FallbackCultureName">The fallback culture name value.</param>
/// <param name="ResourceKey">The resource key value.</param>
/// <param name="PackageId">The package id value.</param>
/// <param name="Scope">The scope value.</param>
/// <param name="CultureRevision">The culture revision value.</param>
/// <param name="ErrorKind">The error kind value.</param>
/// <param name="ContributionId">The contribution id value.</param>
/// <param name="RevokedPackageCount">The revoked package count value.</param>
/// <param name="ScopeId">The scope id value.</param>
/// <param name="ProviderKind">The provider kind value.</param>
/// <param name="Location">The location value.</param>
/// <param name="OperationId">The operation id value.</param>
/// <param name="Attempt">The attempt value.</param>
/// <param name="ElapsedMilliseconds">The elapsed milliseconds value.</param>
public sealed record LocalizationDiagnosticRecord(
    string Code,
    string Message,
    LocalizationDiagnosticSeverity Severity,
    string? CultureName = null,
    string? FallbackCultureName = null,
    string? ResourceKey = null,
    string? PackageId = null,
    ResourceScope? Scope = null,
    long? CultureRevision = null,
    LocalizationErrorKind? ErrorKind = null,
    string? ContributionId = null,
    int? RevokedPackageCount = null,
    string? ScopeId = null,
    LanguagePackageProviderKind? ProviderKind = null,
    string? Location = null,
    string? OperationId = null,
    int? Attempt = null,
    double? ElapsedMilliseconds = null);

/// <summary>
/// Defines the supported localization diagnostic severity values.
/// </summary>
public enum LocalizationDiagnosticSeverity
{
    /// <summary>
    /// Represents the trace value.
    /// </summary>
    Trace,
    /// <summary>
    /// Represents the info value.
    /// </summary>
    Info,
    /// <summary>
    /// Represents the warning value.
    /// </summary>
    Warning,
    /// <summary>
    /// Represents the error value.
    /// </summary>
    Error,
}
