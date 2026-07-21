namespace CallCadence.Application.ApiCall;

/// <summary>
/// Row model for the API editing list.
/// </summary>
public sealed class ApiCallListItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool HasSchedule { get; set; }
    public DateTime? NextScheduledCall { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastErrorAt { get; set; }
}
