namespace CallCadence.Domain.ApiCall;

/// <summary>
/// Single-row table that tracks aggregate API call statistics across all server restarts.
/// </summary>
public sealed class ApiCallStats
{
    public int PkId { get; set; }
    public long TotalApiCalls { get; set; }
    public long TotalSuccessfulCalls { get; set; }
    public DateTime? LastSuccessfulCallAt { get; set; }
    public long TotalErroredCalls { get; set; }
    public DateTime? LastErroredCallAt { get; set; }
    public DateTime? FirstApiCallAt { get; set; }
}
