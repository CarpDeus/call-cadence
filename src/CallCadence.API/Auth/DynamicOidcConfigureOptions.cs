using BugLogger.Interfaces;
using CallCadence.Infrastructure.ApiCall;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OAuth.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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

        // Capture and log failures that occur inside the OIDC middleware while
        // processing the signin-{scheme} (signin-oidc) callback. Without these
        // handlers such failures surface as an unhandled exception with no context.
        var schemeName = config.SchemeName;
        options.Events = new OpenIdConnectEvents
        {
            OnRemoteFailure = context =>
            {
                Redirect(context, LogFailure(context.HttpContext, context.Failure, schemeName, "OIDC remote failure"));
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Redirect(context, LogFailure(context.HttpContext, context.Exception, schemeName, "OIDC authentication failed"));
                return Task.CompletedTask;
            },
            OnAccessDenied = context =>
            {
                Redirect(context, LogFailure(context.HttpContext, null, schemeName, "OIDC access denied"));
                return Task.CompletedTask;
            }
        };
    }

    // Issues the redirect (if a target was resolved) and stops the default rethrow so
    // the user lands on the UI error page instead of an unhandled exception.
    private static void Redirect<TOptions>(HandleRequestContext<TOptions> context, string? errorRedirect)
        where TOptions : AuthenticationSchemeOptions
    {
        if (errorRedirect is null)
        {
            return;
        }

        context.Response.Redirect(errorRedirect);
        context.HandleResponse();
    }

    // Logs the failure to both Sentry and ILogger with a shared reference id and
    // returns the allow-listed UI error redirect (or null when none is configured).
    private static string? LogFailure(
        HttpContext httpContext,
        Exception? exception,
        string schemeName,
        string errorMessage)
    {
        var services = httpContext.RequestServices;
        var reference = Guid.NewGuid().ToString();

        var sentryService = services.GetService<ISentryService>();
        if (sentryService is not null)
        {
            sentryService.SetTag("sso.scheme", schemeName);
            sentryService.AddContext("sso", new
            {
                Scheme = schemeName,
                Reference = reference,
                Detail = exception?.Message ?? errorMessage
            });

            var loggedException = exception ?? new InvalidOperationException(errorMessage);
            sentryService.LogException(loggedException, errorMessage, reference);
        }

        var logger = services.GetService<ILogger<DynamicOidcConfigureOptions>>();
        logger?.LogError(
            exception,
            "{ErrorMessage} for scheme '{Scheme}'. Reference: {Reference}",
            errorMessage,
            schemeName,
            reference);

        var configuration = services.GetService<IConfiguration>();
        return BuildErrorRedirect(configuration, reference);
    }

    // Builds a redirect to the first allow-listed UI URL so we never emit an
    // open redirect. Returns null when no allow-listed URL is configured.
    private static string? BuildErrorRedirect(IConfiguration? configuration, string reference)
    {
        var allowedUrl = configuration?.GetAllowedUiReturnUrls().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(allowedUrl))
        {
            return null;
        }

        return $"{allowedUrl.TrimEnd('/')}/sso-callback?error=sso_failed&reference={Uri.EscapeDataString(reference)}";
    }
}