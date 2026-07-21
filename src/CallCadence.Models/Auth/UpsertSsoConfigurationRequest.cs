namespace CallCadence.Models.Auth;

public sealed class UpsertSsoConfigurationRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? MetadataAddress { get; set; }
    public string? CallbackPath { get; set; }
}
