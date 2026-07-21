using System.Security.Claims;
using System.Text.Encodings.Web;
using CallCadence.API.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace CallCadence.IntegrationTests.TestAuthentication;

public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "IntegrationTest";
    public const string RoleHeaderName = "X-Test-Role";
    public const string UserIdHeaderName = "X-Test-UserId";
    public const string EmailHeaderName = "X-Test-Email";
    public const string AnonymousRole = "Anonymous";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.Equals(Request.Headers[RoleHeaderName], AnonymousRole, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var role = Request.Headers[RoleHeaderName].ToString();
        var userId = Request.Headers[UserIdHeaderName].ToString();
        var email = Request.Headers[EmailHeaderName].ToString();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, string.IsNullOrWhiteSpace(userId) ? "integration-user" : userId),
            new(ClaimTypes.Email, string.IsNullOrWhiteSpace(email) ? "integration@example.com" : email),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(email) ? "integration@example.com" : email)
        };

        if (!string.Equals(role, "User", StringComparison.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, ApplicationRoles.Admin));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
