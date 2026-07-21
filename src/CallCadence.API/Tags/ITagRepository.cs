namespace CallCadence.Domain.Tags;

/// <summary>
/// Repository interface for persisting and querying tags.
/// </summary>
public interface ITagRepository
{
    Task<Tag?> GetByValueAsync(string value);
    Task<IReadOnlyList<Tag>> LookupAsync(string partialValue);
    Task<Tag> CreateAsync(Tag tag);
}
