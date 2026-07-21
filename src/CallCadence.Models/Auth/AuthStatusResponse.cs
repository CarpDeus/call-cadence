namespace CallCadence.Models.Auth;

public sealed class AuthStatusResponse
{
    public bool HasAdmin { get; set; }
    public bool IsAuthenticated { get; set; }
    public string? CurrentUserEmail { get; set; }
    public bool CurrentUserIsAdmin { get; set; }
    public IReadOnlyList<SsoProviderInfo> SsoProviders { get; set; } = [];
}
