namespace CallCadence.Application.Dashboard;

public sealed class DashboardStateDto
{
    public List<ActiveApiCallDto> CurrentActivities { get; set; } = [];
    public long SuccessfulCalls { get; set; }
    public long ErrorCount { get; set; }
    public DateTime ServerStartedAt { get; set; }
    public List<RecentSuccessfulCallDto> RecentSuccessfulCalls { get; set; } = [];
    public List<DashboardErrorDto> Errors { get; set; } = [];
}
