namespace CallCadence.API.Auth;

/// <summary>
/// Configuration helpers that make settings friendly to both appsettings.json
/// and environment-variable based deployments (e.g. Docker/Kubernetes).
/// </summary>
public static class ConfigurationExtensions
{
    private const string AllowedUiReturnUrlsKey = "AllowedUiReturnUrls";
    private static readonly char[] Separators = [',', ';'];

    /// <summary>
    /// Reads the allowlist of UI return URLs used after SSO sign-in.
    /// Supports either a JSON/binding array (AllowedUiReturnUrls:0, AllowedUiReturnUrls__0, ...)
    /// or a single delimited string (comma or semicolon separated) so it can be supplied
    /// through a single environment variable in container deployments.
    /// </summary>
    public static IReadOnlyList<string> GetAllowedUiReturnUrls(this IConfiguration configuration)
    {
        // Array form: appsettings.json array or AllowedUiReturnUrls__0 style env vars.
        var fromArray = configuration.GetSection(AllowedUiReturnUrlsKey).Get<List<string>>();
        if (fromArray is { Count: > 0 })
        {
            return Normalize(fromArray);
        }

        // Delimited-string form: a single AllowedUiReturnUrls env var, e.g.
        // "https://app.example.com,https://admin.example.com".
        var delimited = configuration[AllowedUiReturnUrlsKey];
        if (!string.IsNullOrWhiteSpace(delimited))
        {
            return Normalize(delimited.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return [];
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> urls)
    {
        return urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
