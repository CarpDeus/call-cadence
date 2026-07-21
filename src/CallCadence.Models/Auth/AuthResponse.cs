namespace CallCadence.Models.Auth;

public sealed class AuthResponse
{
    public bool Authenticated { get; set; }
    public string? Email { get; set; }
    public bool IsAdmin { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Token { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
