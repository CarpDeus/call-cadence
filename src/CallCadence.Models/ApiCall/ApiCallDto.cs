using CallCadence.Domain.ApiCall;

namespace CallCadence.Application.ApiCall;

/// <summary>
/// DTO for API call definition responses.
/// </summary>
public sealed class ApiCallDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public List<NamedValue> Headers { get; set; } = [];
    public List<NamedValue> Parameters { get; set; } = [];
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}
