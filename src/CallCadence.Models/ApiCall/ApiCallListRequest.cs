namespace CallCadence.Application.ApiCall;

/// <summary>
/// Query parameters for the API editing list.
/// </summary>
public sealed class ApiCallListRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "title";
    public bool SortDescending { get; set; }
    public bool? Enabled { get; set; }
}
