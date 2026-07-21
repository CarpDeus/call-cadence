using CallCadence.Domain.ApiCall;
using Microsoft.EntityFrameworkCore;

namespace CallCadence.Infrastructure.ApiCall;

/// <summary>
/// SQL Server implementation of IApiCallArchiveRepository.
/// </summary>
public sealed class ApiCallArchiveRepository : IApiCallArchiveRepository
{
    private readonly CallCadenceDbContext _context;

    public ApiCallArchiveRepository(CallCadenceDbContext context)
    {
        _context = context;
    }

    public async Task<ApiCallArchive?> GetByIdAsync(Guid id)
    {
        return await _context.ApiCallArchives.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IEnumerable<ApiCallArchive>> GetAllAsync()
    {
        return await _context.ApiCallArchives
            .OrderByDescending(x => x.ArchivedAt)
            .ToListAsync();
    }

    public async Task<ApiCallArchive> CreateAsync(ApiCallArchive archive)
    {
        _context.ApiCallArchives.Add(archive);
        await _context.SaveChangesAsync();
        return archive;
    }

    public async Task<ApiCallArchive> UpdateAsync(ApiCallArchive archive)
    {
        var existingArchive = await _context.ApiCallArchives.FirstOrDefaultAsync(x => x.Id == archive.Id);
        if (existingArchive is null)
        {
            return archive;
        }

        _context.Entry(existingArchive).CurrentValues.SetValues(archive);
        await _context.SaveChangesAsync();
        return existingArchive;
    }

    public async Task DeleteAsync(Guid id)
    {
        var archive = await _context.ApiCallArchives.FirstOrDefaultAsync(x => x.Id == id);
        if (archive is null)
        {
            return;
        }

        _context.ApiCallArchives.Remove(archive);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<ApiCallArchive>> GetByApiCallIdAsync(Guid apiCallId)
    {
        return await _context.ApiCallArchives
            .Where(x => x.ApiCallId == apiCallId)
            .OrderByDescending(x => x.ArchivedAt)
            .ToListAsync();
    }
}
