namespace CallCadence.API.Auth;

public sealed class SsoConfiguration
{
    public long PkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SchemeName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? MetadataAddress { get; set; }
    public string? CallbackPath { get; set; }
    public DateTime UpdatedAt { get; set; }
}
