using CallCadence.Models.Auth;

namespace CallCadence.API.Auth;

public interface ISsoConfigurationService
{
    /// <summary>
    /// Returns <c>true</c> when SSO configuration is sourced from the
    /// <c>CALLCADENCE_SSO_CONFIG</c> environment variable rather than the database.
    /// When <c>true</c>, write operations (<see cref="UpsertAsync"/>, <see cref="DeleteAsync"/>)
    /// throw <see cref="InvalidOperationException"/>.
    /// </summary>
    bool IsOverriddenByEnvironment { get; }

    Task<IReadOnlyList<SsoConfigurationResponse>> GetAllAsync();
    Task<SsoConfigurationResponse?> GetBySchemeNameAsync(string schemeName);
    Task<SsoConfigurationResponse> UpsertAsync(UpsertSsoConfigurationRequest request);
    Task DeleteAsync(string schemeName);
    Task<bool> IsEnabledAsync();
}
