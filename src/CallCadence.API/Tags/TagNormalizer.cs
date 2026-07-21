using System.Text.RegularExpressions;

namespace CallCadence.Application.Tags;

public static partial class TagNormalizer
{
    public static string Normalize(string value)
    {
        var normalizedBody = NormalizeBody(value);
        if (string.IsNullOrWhiteSpace(normalizedBody))
        {
            throw new ArgumentException("Tag must contain at least one non-space character.", nameof(value));
        }

        return $"#{normalizedBody}";
    }

    public static string NormalizePartial(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmedValue = value.Trim();
        var hadHashPrefix = trimmedValue.StartsWith('#');
        var normalizedBody = NormalizeBody(trimmedValue);
        if (string.IsNullOrWhiteSpace(normalizedBody))
        {
            return string.Empty;
        }

        return hadHashPrefix ? $"#{normalizedBody}" : normalizedBody;
    }

    private static string NormalizeBody(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var trimmedValue = value.Trim().TrimStart('#');
        var collapsedWhitespace = MultiWhitespace().Replace(trimmedValue, "_");
        return collapsedWhitespace.Trim('_').ToLowerInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiWhitespace();
}
