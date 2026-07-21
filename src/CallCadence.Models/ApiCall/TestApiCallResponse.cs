using CallCadence.Domain.ApiCall;

namespace CallCadence.Application.ApiCall;

/// <summary>
/// Response DTO for a test API call execution.
/// </summary>
public sealed class TestApiCallResponse
{
    public string? RequestUri { get; set; }
    public List<NamedValue> RequestHeaders { get; set; } = [];
    public List<NamedValue> RequestParameters { get; set; } = [];
    public string? RequestBody { get; set; }
    public int ResponseCode { get; set; }
    public string? ResponseBody { get; set; }
    public long DurationMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
