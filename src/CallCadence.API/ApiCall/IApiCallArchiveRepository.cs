namespace CallCadence.Domain.ApiCall;

/// <summary>
/// Repository interface for managing API call archive records.
/// </summary>
public interface IApiCallArchiveRepository
{
    Task<ApiCallArchive?> GetByIdAsync(Guid id);
    Task<IEnumerable<ApiCallArchive>> GetAllAsync();
    Task<ApiCallArchive> CreateAsync(ApiCallArchive archive);
    Task<ApiCallArchive> UpdateAsync(ApiCallArchive archive);
    Task DeleteAsync(Guid id);
    Task<IEnumerable<ApiCallArchive>> GetByApiCallIdAsync(Guid apiCallId);
}
