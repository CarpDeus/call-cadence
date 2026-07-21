namespace CallCadence.API.Auth;

public interface IJwtTokenService
{
    string GenerateToken(AdminUser user, IList<string> roles);
}
