namespace CallCadence.Domain.ApiCall;

/// <summary>
/// Response model containing scheduled job information with API call details.
/// </summary>
public sealed class ScheduleInfoResponse
{
    public Guid ScheduleId { get; set; }
    public string JobId { get; set; } = string.Empty;
    public Guid ApiCallId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime? NextExecution { get; set; }
    public string CronExpression { get; set; } = string.Empty;
}
