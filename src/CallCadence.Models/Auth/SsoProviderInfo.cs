namespace CallCadence.Models.Auth;

public sealed class SsoProviderInfo
{
    public string Name { get; set; } = string.Empty;
    public string SchemeName { get; set; } = string.Empty;
    public string SignInUrl { get; set; } = string.Empty;
}
