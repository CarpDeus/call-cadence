namespace CallCadence.Application.Tags;

/// <summary>
/// DTO for adding a tag.
/// </summary>
public sealed class CreateTagDto
{
    public string Value { get; set; } = string.Empty;
}
