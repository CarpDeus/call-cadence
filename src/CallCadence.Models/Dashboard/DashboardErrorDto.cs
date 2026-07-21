namespace CallCadence.Application.Dashboard;

public sealed class DashboardErrorDto
{
    public Guid Id { get; set; }
    public Guid ApiCallId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
