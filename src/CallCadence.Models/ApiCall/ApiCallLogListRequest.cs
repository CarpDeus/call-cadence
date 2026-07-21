namespace CallCadence.Application.ApiCall;

/// <summary>
/// Query parameters for paged API call log results.
/// </summary>
public sealed class ApiCallLogListRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "executedAt";
    public bool SortDescending { get; set; } = true;
}
