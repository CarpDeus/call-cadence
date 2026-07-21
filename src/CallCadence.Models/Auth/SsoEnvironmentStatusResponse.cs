namespace CallCadence.Models.Auth;

public sealed class SsoEnvironmentStatusResponse
{
    /// <summary>
    /// <c>true</c> when SSO configuration is sourced from the
    /// <c>CALLCADENCE_SSO_CONFIG</c> environment variable.
    /// When <c>true</c> the UI should treat all provider settings as read-only.
    /// </summary>
    public bool IsOverriddenByEnvironment { get; set; }
}
