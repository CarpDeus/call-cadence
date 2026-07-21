namespace CallCadence.Domain.Tags;

/// <summary>
/// Represents a normalized tag stored for lookup and filtering.
/// </summary>
public sealed class Tag
{
    public long PkId { get; set; }
    public string Value { get; set; } = string.Empty;
}
