namespace CallCadence.Domain.ApiCall;

/// <summary>
/// Represents an API call definition with all necessary details for execution.
/// </summary>
public sealed class ApiCall
{
    public long PkId { get; set; }
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "GET";
    public string EndpointUrl { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public List<NamedValue> Headers { get; set; } = [];
    public List<NamedValue> Parameters { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}
