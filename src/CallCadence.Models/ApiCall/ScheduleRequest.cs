namespace CallCadence.Domain.ApiCall;

/// <summary>
/// Request model for scheduling an API call with a cron expression.
/// </summary>
public sealed class ScheduleRequest
{
    public Guid? ScheduleId { get; set; }
    public Guid ApiCallId { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
