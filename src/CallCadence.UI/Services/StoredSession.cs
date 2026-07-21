namespace CallCadence.UI.Services;

public sealed class StoredSession
{
    public string? Token { get; set; }
    public string? Email { get; set; }
    public bool IsAdmin { get; set; }
    public string? ExpiresAtUtc { get; set; }
}
