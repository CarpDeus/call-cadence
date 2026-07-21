using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CallCadence.API.Auth;

/// <summary>
/// Reads SSO provider configuration from the <c>CALLCADENCE_SSO_CONFIG</c> environment variable.
/// When present, this completely replaces the database-backed configuration.
/// The variable must contain a JSON array of provider objects.
/// </summary>
public static partial class EnvVarSsoConfigurationProvider
{
    public const string EnvironmentVariableName = "CALLCADENCE_SSO_CONFIG";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Attempts to load SSO configurations from the environment variable.
    /// Returns <c>null</c> if the variable is absent or empty.
    /// Throws <see cref="InvalidOperationException"/> if the JSON is malformed or
    /// contains entries that fail validation.
    /// </summary>
    public static IReadOnlyList<SsoConfiguration>? TryLoad()
    {
        var raw = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        List<EnvVarSsoEntry> entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<EnvVarSsoEntry>>(raw, JsonOptions)
                ?? throw new InvalidOperationException(
                    $"Environment variable {EnvironmentVariableName} deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Environment variable {EnvironmentVariableName} contains invalid JSON: {ex.Message}", ex);
        }

        var result = new List<SsoConfiguration>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            Validate(entry, i);

            var schemeName = string.IsNullOrWhiteSpace(entry.SchemeName)
                ? DeriveSchemeNameFromProviderName(entry.Name!)
                : entry.SchemeName.Trim();

            result.Add(new SsoConfiguration
            {
                PkId = 0,
                Name = entry.Name!.Trim(),
                SchemeName = schemeName,
                IsEnabled = entry.IsEnabled,
                Authority = Normalize(entry.Authority),
                ClientId = Normalize(entry.ClientId),
                ClientSecret = Normalize(entry.ClientSecret),
                MetadataAddress = Normalize(entry.MetadataAddress),
                CallbackPath = Normalize(entry.CallbackPath),
                UpdatedAt = DateTime.UtcNow
            });
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Derives a URL-safe authentication scheme name from a human-readable provider name.
    /// For example, "Azure AD" becomes "oidc-azure-ad".
    /// </summary>
    public static string DeriveSchemeNameFromProviderName(string name)
    {
        var slug = NonAlphanumericOrSpaceRegex().Replace(name.Trim(), string.Empty);
        slug = WhitespaceRegex().Replace(slug, "-").ToLowerInvariant();
        slug = LeadingTrailingDashRegex().Replace(slug, string.Empty);
        return $"oidc-{slug}";
    }

    private static void Validate(EnvVarSsoEntry entry, int index)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
            throw new InvalidOperationException(
                $"Environment variable {EnvironmentVariableName}: entry at index {index} is missing 'name'.");

        if (string.IsNullOrWhiteSpace(entry.ClientId))
            throw new InvalidOperationException(
                $"Environment variable {EnvironmentVariableName}: entry '{entry.Name}' is missing 'clientId'.");

        if (string.IsNullOrWhiteSpace(entry.Authority) && string.IsNullOrWhiteSpace(entry.MetadataAddress))
            throw new InvalidOperationException(
                $"Environment variable {EnvironmentVariableName}: entry '{entry.Name}' must have either 'authority' or 'metadataAddress'.");
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"[^a-zA-Z0-9 ]")]
    private static partial Regex NonAlphanumericOrSpaceRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^-+|-+$")]
    private static partial Regex LeadingTrailingDashRegex();
}

/// <summary>
/// JSON deserialization model for a single SSO provider entry in the environment variable.
/// </summary>
internal sealed class EnvVarSsoEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("schemeName")]
    public string? SchemeName { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("authority")]
    public string? Authority { get; set; }

    [JsonPropertyName("clientId")]
    public string? ClientId { get; set; }

    [JsonPropertyName("clientSecret")]
    public string? ClientSecret { get; set; }

    [JsonPropertyName("metadataAddress")]
    public string? MetadataAddress { get; set; }

    [JsonPropertyName("callbackPath")]
    public string? CallbackPath { get; set; }
}
