namespace CallCadence.Models.Auth;

public sealed class UserSummaryResponse
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsDeactivated { get; set; }
}
