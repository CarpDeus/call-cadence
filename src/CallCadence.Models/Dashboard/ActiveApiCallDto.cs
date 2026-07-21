namespace CallCadence.Application.Dashboard;

public sealed class ActiveApiCallDto
{
    public Guid ApiCallId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
}
