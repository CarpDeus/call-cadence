namespace CallCadence.Domain.ApiCall;

/// <summary>
/// Logs each execution of an API call with response details.
/// </summary>
public sealed class ApiCallLog
{
    public long PkId { get; set; }
    public Guid Id { get; set; }
    public Guid ApiCallId { get; set; }
    public string HttpMethod { get; set; } = "GET";
    public string? RequestUri { get; set; }
    public List<NamedValue> RequestHeaders { get; set; } = [];
    public List<NamedValue> RequestParameters { get; set; } = [];
    public string? RequestBody { get; set; }
    public int ResponseCode { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime ExecutedAt { get; set; }
    public long DurationMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
