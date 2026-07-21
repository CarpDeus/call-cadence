using CallCadence.Domain.Tags;
using CallCadence.Infrastructure.ApiCall;
using Microsoft.EntityFrameworkCore;

namespace CallCadence.Infrastructure.Tags;

/// <summary>
/// SQL Server implementation of ITagRepository.
/// </summary>
public sealed class TagRepository : ITagRepository
{
    private readonly CallCadenceDbContext _context;

    public TagRepository(CallCadenceDbContext context)
    {
        _context = context;
    }

    public async Task<Tag?> GetByValueAsync(string value)
    {
        return await _context.Tags.AsNoTracking().FirstOrDefaultAsync(tag => tag.Value == value);
    }

    public async Task<IReadOnlyList<Tag>> LookupAsync(string partialValue)
    {
        var query = _context.Tags.AsNoTracking().OrderBy(tag => tag.Value).AsQueryable();

        if (!string.IsNullOrWhiteSpace(partialValue))
        {
            query = query.Where(tag => tag.Value.Contains(partialValue));
        }

        return await query.ToListAsync();
    }

    public async Task<Tag> CreateAsync(Tag tag)
    {
        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();
        return tag;
    }
}
