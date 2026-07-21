using CallCadence.Domain.ApiCall;

namespace CallCadence.Application.ApiCall;

/// <summary>
/// Request DTO for testing an API call definition without saving it.
/// </summary>
public sealed class TestApiCallRequest
{
    public string HttpMethod { get; set; } = "GET";
    public string EndpointUrl { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public List<NamedValue> Headers { get; set; } = [];
    public List<NamedValue> Parameters { get; set; } = [];
}
