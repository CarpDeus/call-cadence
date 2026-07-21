using CallCadence.Application.ApiCall;
using CallCadence.Domain.Paging;

namespace CallCadence.Domain.ApiCall;

/// <summary>
/// Repository interface for managing API call definitions.
/// </summary>
public interface IApiCallRepository
{
    Task<ApiCall?> GetByIdAsync(Guid id);
    Task<IEnumerable<ApiCall>> GetAllAsync();
    Task<IEnumerable<ApiCall>> GetActiveAsync();
    Task<PagedResult<ApiCallListItemDto>> GetListPageAsync(ApiCallListRequest request);
    Task<IReadOnlyList<ApiCallListItemDto>> GetListItemsAsync(bool? enabled);
    Task<ApiCall> CreateAsync(ApiCall apiCall);
    Task<ApiCall> UpdateAsync(ApiCall apiCall);
    Task DeleteAsync(Guid id);
}
