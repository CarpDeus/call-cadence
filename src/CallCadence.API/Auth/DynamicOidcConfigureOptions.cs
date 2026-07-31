using CallCadence.Infrastructure.ApiCall;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authentication;

namespace CallCadence.API.Auth;

/// <summary>
/// Configures OIDC authentication options for each named provider scheme from either
/// the environment variable override or the persisted SSO configuration.
/// Options are resolved once when first used; a service restart is required to
/// pick up subsequent changes to the SSO configuration.
/// </summary>
internal sealed class DynamicOidcConfigureOptions : IConfigureNamedOptions<OpenIdConnectOptions>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReadOnlyList<SsoConfiguration>? _envConfigs;

    public DynamicOidcConfigureOptions(IServiceScopeFactory scopeFactory,
        IReadOnlyList<SsoConfiguration>? envConfigs = null)
    {
        _scopeFactory = scopeFactory;
        _envConfigs = envConfigs;
    }

    public void Configure(string? name, OpenIdConnectOptions options)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var config = ResolveConfig(name);
        if (config is null || !config.IsEnabled)
            return;

        ApplyConfig(config, options);
    }

    public void Configure(OpenIdConnectOptions options) { }

    private SsoConfiguration? ResolveConfig(string schemeName)
    {
        if (_envConfigs is not null)
        {
            return _envConfigs.FirstOrDefault(c =>
                string.Equals(c.SchemeName, schemeName, StringComparison.OrdinalIgnoreCase));
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<CallCadenceDbContext>();
        if (dbContext is null)
            return null;

        return dbContext.SsoConfigurations
            .FirstOrDefault(c => c.SchemeName == schemeName);
    }

    private static void ApplyConfig(SsoConfiguration config, OpenIdConnectOptions options)
    {
        if (!string.IsNullOrWhiteSpace(config.Authority))
            options.Authority = config.Authority;
        if (!string.IsNullOrWhiteSpace(config.MetadataAddress))
            options.MetadataAddress = config.MetadataAddress;
        if (!string.IsNullOrWhiteSpace(config.ClientId))
            options.ClientId = config.ClientId;
        if (!string.IsNullOrWhiteSpace(config.ClientSecret))
            options.ClientSecret = config.ClientSecret;
        if (!string.IsNullOrWhiteSpace(config.CallbackPath))
            options.CallbackPath = config.CallbackPath;

        // OIDC establishes the external identity in the Identity external cookie.
        // The sso-callback endpoint reads this same scheme to complete sign-in.
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;

        // Request the standard scopes. "email" must be requested explicitly or
        // providers such as Authentik will not return the email claim, which the
        // sso-callback endpoint requires to map the external identity to a user.
        if (!options.Scope.Contains("openid"))
            options.Scope.Add("openid");
        if (!options.Scope.Contains("profile"))
            options.Scope.Add("profile");
        if (!options.Scope.Contains("email"))
            options.Scope.Add("email");

        // Ensure the "email" claim from the userinfo/id_token is surfaced as the
        // standard ClaimTypes.Email that sso-callback looks up first.
        options.ClaimActions.MapUniqueJsonKey(ClaimTypes.Email, "email");
    }
}