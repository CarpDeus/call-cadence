using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CallCadence.Infrastructure.ApiCall;

/// <summary>
/// Shared configuration for authorizing access to the Hangfire Dashboard.
/// The dashboard is protected by a dedicated cookie scheme that is issued by a
/// handshake endpoint after validating a UI-issued JWT. Centralizing the token
/// validation parameters here keeps the JWT bearer registration and the handshake
/// endpoint in agreement about issuer/audience/signing-key.
/// </summary>
public static class HangfireDashboardAuth
{
    /// <summary>
    /// Name of the cookie authentication scheme used exclusively for the Hangfire Dashboard.
    /// </summary>
    public const string CookieScheme = "Hangfire";

    /// <summary>
    /// Builds the <see cref="TokenValidationParameters"/> used to validate JWTs for both
    /// the JWT bearer scheme and the Hangfire handshake endpoint.
    /// </summary>
    public static TokenValidationParameters BuildTokenValidationParameters(IConfiguration configuration)
    {
        var signingKey = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey configuration is required. " +
                "Set the Jwt__SigningKey environment variable or add Jwt:SigningKey to appsettings.json.");
        }

        var issuer = configuration["Jwt:Issuer"];
        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException("Jwt:Issuer configuration is required.");
        }

        var audience = configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("Jwt:Audience configuration is required.");
        }

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
        };
    }
}
