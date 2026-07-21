namespace CallCadence.Application.Dashboard;

public sealed class DashboardCallCompletedDto
{
    public Guid ApiCallId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DashboardErrorDto? Error { get; set; }
}
