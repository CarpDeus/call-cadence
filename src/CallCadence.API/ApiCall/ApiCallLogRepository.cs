using CallCadence.Domain.ApiCall;
using Microsoft.EntityFrameworkCore;

namespace CallCadence.Infrastructure.ApiCall;

/// <summary>
/// SQL Server implementation of IApiCallLogRepository.
/// </summary>
public sealed class ApiCallLogRepository : IApiCallLogRepository
{
    private readonly CallCadenceDbContext _context;

    public ApiCallLogRepository(CallCadenceDbContext context)
    {
        _context = context;
    }

    public async Task<ApiCallLog?> GetByIdAsync(Guid id)
    {
        return await _context.ApiCallLogs.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IEnumerable<ApiCallLog>> GetAllAsync()
    {
        return await _context.ApiCallLogs
            .OrderByDescending(x => x.ExecutedAt)
            .ToListAsync();
    }

    public async Task<ApiCallLog> CreateAsync(ApiCallLog log)
    {
        _context.ApiCallLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task<ApiCallLog> UpdateAsync(ApiCallLog log)
    {
        var existingLog = await _context.ApiCallLogs.FirstOrDefaultAsync(x => x.Id == log.Id);
        if (existingLog is null)
        {
            return log;
        }

        _context.Entry(existingLog).CurrentValues.SetValues(log);
        await _context.SaveChangesAsync();
        return existingLog;
    }

    public async Task DeleteAsync(Guid id)
    {
        var log = await _context.ApiCallLogs.FirstOrDefaultAsync(x => x.Id == id);
        if (log is null)
        {
            return;
        }

        _context.ApiCallLogs.Remove(log);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ApiCallLog>> GetByApiCallIdAsync(Guid apiCallId)
    {
        return await _context.ApiCallLogs
            .Where(x => x.ApiCallId == apiCallId)
            .OrderByDescending(x => x.ExecutedAt)
            .ToListAsync();
    }

}
