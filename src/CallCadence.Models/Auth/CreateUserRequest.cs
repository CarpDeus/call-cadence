namespace CallCadence.Models.Auth;

public sealed class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}
