using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace CallCadence.IntegrationTests.TestAuthentication;

internal static class TestAuthenticationExtensions
{
    public static IServiceCollection AddTestAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
            options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            options.DefaultScheme = TestAuthenticationHandler.SchemeName;
        }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
            TestAuthenticationHandler.SchemeName,
            _ => { });

        return services;
    }
}
