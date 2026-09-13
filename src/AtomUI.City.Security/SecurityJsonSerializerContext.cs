using System.Text.Json.Serialization;

namespace AtomUI.City.Security;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FileAccountSessionStore.AccountDocument))]
[JsonSerializable(typeof(FileAccountSessionStore.ActiveAccountDocument))]
[JsonSerializable(typeof(FileCredentialStore.CredentialDocument))]
internal sealed partial class SecurityJsonSerializerContext : JsonSerializerContext;
