namespace CallCadence.Application.Dashboard;

public sealed class ClearDashboardErrorsRequest
{
    public List<Guid> ErrorIds { get; set; } = [];
}
