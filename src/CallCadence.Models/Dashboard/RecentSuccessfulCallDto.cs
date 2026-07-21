namespace CallCadence.Application.Dashboard;

public sealed class RecentSuccessfulCallDto
{
    public Guid ApiCallId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
}
