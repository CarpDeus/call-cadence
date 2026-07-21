using CallCadence.Domain.ApiCall;

namespace CallCadence.Application.ApiCall;

/// <summary>
/// DTO for creating a new API call definition.
/// </summary>
public sealed class CreateApiCallDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "GET";
    public string EndpointUrl { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public List<NamedValue> Headers { get; set; } = [];
    public List<NamedValue> Parameters { get; set; } = [];
    public bool IsActive { get; set; } = true;
}
