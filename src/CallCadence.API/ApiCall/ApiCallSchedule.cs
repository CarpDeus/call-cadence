namespace CallCadence.Domain.ApiCall;

/// <summary>
/// Represents a persisted schedule definition for an API call.
/// </summary>
public sealed class ApiCallSchedule
{
    public long PkId { get; set; }
    public Guid Id { get; set; }
    public Guid ApiCallId { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}
