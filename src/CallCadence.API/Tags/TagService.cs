using CallCadence.Domain.Tags;

namespace CallCadence.Application.Tags;

/// <summary>
/// Application service for adding and looking up tags.
/// </summary>
public sealed class TagService
{
    private readonly ITagRepository _tagRepository;

    public TagService(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    public async Task<TagDto> AddAsync(string value)
    {
        var normalizedValue = TagNormalizer.Normalize(value);
        var existingTag = await _tagRepository.GetByValueAsync(normalizedValue);
        if (existingTag is not null)
        {
            return MapToDto(existingTag);
        }

        var createdTag = await _tagRepository.CreateAsync(new Tag
        {
            Value = normalizedValue
        });

        return MapToDto(createdTag);
    }

    public async Task<IReadOnlyList<TagDto>> LookupAsync(string? partialValue)
    {
        var normalizedPartial = TagNormalizer.NormalizePartial(partialValue);
        var tags = await _tagRepository.LookupAsync(normalizedPartial);
        return tags.Select(MapToDto).ToList();
    }

    private static TagDto MapToDto(Tag tag) => new()
    {
        Value = tag.Value
    };
}
