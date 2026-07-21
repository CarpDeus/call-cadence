namespace CallCadence.UI.Services;

public sealed class UserSessionState
{
    public bool IsAuthenticated { get; private set; }
    public string? Email { get; private set; }
    public bool IsAdmin { get; private set; }
    public string? Token { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }

    public void SignIn(string email, bool isAdmin, string? token = null, DateTime? expiresAtUtc = null)
    {
        IsAuthenticated = true;
        Email = email;
        IsAdmin = isAdmin;
        Token = token;
        ExpiresAtUtc = expiresAtUtc;
    }

    public void SignOut()
    {
        IsAuthenticated = false;
        Email = null;
        IsAdmin = false;
        Token = null;
        ExpiresAtUtc = null;
    }
}
