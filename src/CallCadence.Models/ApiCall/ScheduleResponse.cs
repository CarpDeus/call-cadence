namespace CallCadence.Domain.ApiCall;

/// <summary>
/// Response model for scheduling an API call.
/// </summary>
public sealed class ScheduleResponse
{
    public Guid ScheduleId { get; set; }
    public string JobId { get; set; } = string.Empty;
    public Guid ApiCallId { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Message { get; set; } = string.Empty;
}
