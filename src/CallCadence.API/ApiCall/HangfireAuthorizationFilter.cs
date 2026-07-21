using CallCadence.API.Auth;
using Hangfire.Dashboard;

namespace CallCadence.Infrastructure.ApiCall;

/// <summary>
/// Authorization filter for Hangfire Dashboard.
/// Restricts Hangfire Dashboard access to authenticated administrators.
/// </summary>
public sealed class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole(ApplicationRoles.Admin);
    }
}
