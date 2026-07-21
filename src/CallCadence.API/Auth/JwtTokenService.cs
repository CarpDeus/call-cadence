using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CallCadence.API.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(AdminUser user, IList<string> roles)
    {
        var signingKey = _configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException(
                "Jwt:SigningKey configuration is required. " +
                "Set the Jwt__SigningKey environment variable or add Jwt:SigningKey to appsettings.json.");

        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer configuration is required.");

        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience configuration is required.");

        var expiryMinutes = _configuration.GetValue<int>("Jwt:ExpiryMinutes", 60);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
