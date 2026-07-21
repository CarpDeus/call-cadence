namespace CallCadence.Domain.ApiCall;

/// <summary>
/// Repository interface for managing API call execution logs.
/// </summary>
public interface IApiCallLogRepository
{
    Task<ApiCallLog?> GetByIdAsync(Guid id);
    Task<IEnumerable<ApiCallLog>> GetAllAsync();
    Task<ApiCallLog> CreateAsync(ApiCallLog log);
    Task<ApiCallLog> UpdateAsync(ApiCallLog log);
    Task DeleteAsync(Guid id);
    Task<IEnumerable<ApiCallLog>> GetByApiCallIdAsync(Guid apiCallId);
}
