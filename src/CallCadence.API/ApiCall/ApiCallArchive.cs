namespace CallCadence.Domain.ApiCall;

/// <summary>
/// Stores previous versions of API call definitions whenever they are modified.
/// </summary>
public sealed class ApiCallArchive
{
    public long PkId { get; set; }
    public Guid Id { get; set; }
    public Guid ApiCallId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "GET";
    public string EndpointUrl { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public List<NamedValue> Headers { get; set; } = [];
    public List<NamedValue> Parameters { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTime ArchivedAt { get; set; }
    public DateTime OriginalCreatedAt { get; set; }
    public DateTime OriginalModifiedAt { get; set; }
}
