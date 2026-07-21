using CallCadence.Domain.ApiCall;
using CallCadence.Domain.Paging;
using CallCadence.Application.ApiCall;
using Microsoft.EntityFrameworkCore;

namespace CallCadence.Infrastructure.ApiCall;

/// <summary>
/// SQL Server implementation of IApiCallRepository.
/// </summary>
public sealed class ApiCallRepository : IApiCallRepository
{
    private readonly CallCadenceDbContext _context;

    public ApiCallRepository(CallCadenceDbContext context)
    {
        _context = context;
    }

    public async Task<Domain.ApiCall.ApiCall?> GetByIdAsync(Guid id)
    {
        return await _context.ApiCalls.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IEnumerable<Domain.ApiCall.ApiCall>> GetAllAsync()
    {
        return await _context.ApiCalls.ToListAsync();
    }

    public async Task<IEnumerable<Domain.ApiCall.ApiCall>> GetActiveAsync()
    {
        return await _context.ApiCalls.Where(x => x.IsActive).ToListAsync();
    }

    public async Task<PagedResult<ApiCallListItemDto>> GetListPageAsync(ApiCallListRequest request)
    {
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 10 : Math.Min(request.PageSize, 100);

        var filteredQuery = BuildListQuery(request.Enabled);
        var totalItems = await filteredQuery.CountAsync();
        var items = await ApplyDatabaseSorting(filteredQuery, request.SortBy, request.SortDescending)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        var pageCount = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

        return new PagedResult<ApiCallListItemDto>(
            new Paging(pageCount, totalItems, pageNumber, pageSize),
            items);
    }

    public async Task<IReadOnlyList<ApiCallListItemDto>> GetListItemsAsync(bool? enabled)
    {
        return await BuildListQuery(enabled).ToListAsync();
    }

    public async Task<Domain.ApiCall.ApiCall> CreateAsync(Domain.ApiCall.ApiCall apiCall)
    {
        _context.ApiCalls.Add(apiCall);
        await _context.SaveChangesAsync();
        return apiCall;
    }

    public async Task<Domain.ApiCall.ApiCall> UpdateAsync(Domain.ApiCall.ApiCall apiCall)
    {
        _context.ApiCalls.Update(apiCall);
        await _context.SaveChangesAsync();
        return apiCall;
    }

    public async Task DeleteAsync(Guid id)
    {
        var apiCall = await _context.ApiCalls.FirstOrDefaultAsync(x => x.Id == id);
        if (apiCall is null)
        {
            return;
        }

        _context.ApiCalls.Remove(apiCall);
        await _context.SaveChangesAsync();
    }

    private IQueryable<ApiCallListItemDto> BuildListQuery(bool? enabled)
    {
        var query = _context.ApiCalls
            .AsNoTracking();

        if (enabled.HasValue)
        {
            query = query.Where(apiCall => apiCall.IsActive == enabled.Value);
        }

        return query.Select(apiCall => new ApiCallListItemDto
        {
            Id = apiCall.Id,
            Title = apiCall.Title,
            IsActive = apiCall.IsActive,
            HasSchedule = false,
            NextScheduledCall = null,
            LastSuccessAt = _context.ApiCallLogs
                .Where(log => log.ApiCallId == apiCall.Id && log.Success)
                .Select(log => (DateTime?)log.ExecutedAt)
                .Max(),
            LastErrorAt = _context.ApiCallLogs
                .Where(log => log.ApiCallId == apiCall.Id && !log.Success)
                .Select(log => (DateTime?)log.ExecutedAt)
                .Max()
        });
    }

    private static IQueryable<ApiCallListItemDto> ApplyDatabaseSorting(
        IQueryable<ApiCallListItemDto> query,
        string? sortBy,
        bool sortDescending)
    {
        var normalizedSortBy = string.IsNullOrWhiteSpace(sortBy) ? "title" : sortBy.Trim().ToLowerInvariant();

        return (normalizedSortBy, sortDescending) switch
        {
            ("enabled", false) => query.OrderBy(item => item.IsActive).ThenBy(item => item.Title),
            ("enabled", true) => query.OrderByDescending(item => item.IsActive).ThenBy(item => item.Title),
            ("lastsuccessat", false) => query
                .OrderBy(item => item.LastSuccessAt == null)
                .ThenBy(item => item.LastSuccessAt)
                .ThenBy(item => item.Title),
            ("lastsuccessat", true) => query
                .OrderBy(item => item.LastSuccessAt == null)
                .ThenByDescending(item => item.LastSuccessAt)
                .ThenBy(item => item.Title),
            ("lasterrorat", false) => query
                .OrderBy(item => item.LastErrorAt == null)
                .ThenBy(item => item.LastErrorAt)
                .ThenBy(item => item.Title),
            ("lasterrorat", true) => query
                .OrderBy(item => item.LastErrorAt == null)
                .ThenByDescending(item => item.LastErrorAt)
                .ThenBy(item => item.Title),
            (_, true) => query.OrderByDescending(item => item.Title).ThenBy(item => item.Id),
            _ => query.OrderBy(item => item.Title).ThenBy(item => item.Id)
        };
    }
}
