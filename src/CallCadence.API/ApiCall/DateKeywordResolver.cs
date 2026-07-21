namespace CallCadence.Infrastructure.ApiCall;

/// <summary>
/// Resolves date keywords to DateTime values.
/// </summary>
public static class DateKeywordResolver
{
    private static readonly HashSet<string> DayNames =
    [
        "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"
    ];

    /// <summary>
    /// Attempts to resolve a date keyword to a DateTime value.
    /// </summary>
    /// <param name="keyword">The keyword to resolve (case-insensitive).</param>
    /// <param name="result">The resolved DateTime value if successful.</param>
    /// <returns>True if the keyword was resolved; otherwise, false.</returns>
    public static bool TryResolveKeyword(string keyword, out DateTime result)
    {
        var lowerKeyword = keyword.ToLowerInvariant();
        var utcNow = DateTime.UtcNow;

        result = lowerKeyword switch
        {
            "today" => utcNow.Date,
            "now" or "utcnow" => utcNow,
            "yesterday" => utcNow.Date.AddDays(-1),
            "tomorrow" => utcNow.Date.AddDays(1),
            _ => ResolveComplexKeyword(lowerKeyword, utcNow)
        };

        return result != DateTime.MinValue;
    }

    private static DateTime ResolveComplexKeyword(string keyword, DateTime reference)
    {
        // Try day-of-week patterns: "monday", "nextMonday", "lastTuesday"
        if (TryResolveDayOfWeek(keyword, reference, out var dayResult))
        {
            return dayResult;
        }

        return DateTime.MinValue; // Indicates resolution failure
    }

    private static bool TryResolveDayOfWeek(string keyword, DateTime reference, out DateTime result)
    {
        result = DateTime.MinValue;

        // Check for "next" or "last" prefix
        var isNext = keyword.StartsWith("next", StringComparison.Ordinal);
        var isLast = keyword.StartsWith("last", StringComparison.Ordinal);

        string dayName;
        if (isNext)
        {
            dayName = keyword[4..]; // Remove "next"
        }
        else if (isLast)
        {
            dayName = keyword[4..]; // Remove "last"
        }
        else
        {
            dayName = keyword;
        }

        if (!DayNames.Contains(dayName))
        {
            return false;
        }

        var targetDay = ParseDayOfWeek(dayName);

        if (isNext)
        {
            result = GetNextDayOfWeek(reference, targetDay);
        }
        else if (isLast)
        {
            result = GetLastDayOfWeek(reference, targetDay);
        }
        else
        {
            // Plain day name: return current or next occurrence
            result = GetCurrentOrNextDayOfWeek(reference, targetDay);
        }

        return true;
    }

    private static DayOfWeek ParseDayOfWeek(string dayName) => dayName switch
    {
        "monday" => DayOfWeek.Monday,
        "tuesday" => DayOfWeek.Tuesday,
        "wednesday" => DayOfWeek.Wednesday,
        "thursday" => DayOfWeek.Thursday,
        "friday" => DayOfWeek.Friday,
        "saturday" => DayOfWeek.Saturday,
        "sunday" => DayOfWeek.Sunday,
        _ => throw new ArgumentException($"Invalid day name: {dayName}", nameof(dayName))
    };

    private static DateTime GetCurrentOrNextDayOfWeek(DateTime reference, DayOfWeek targetDay)
    {
        var current = reference.Date;
        var daysUntilTarget = ((int)targetDay - (int)current.DayOfWeek + 7) % 7;
        return current.AddDays(daysUntilTarget);
    }

    private static DateTime GetNextDayOfWeek(DateTime reference, DayOfWeek targetDay)
    {
        var current = reference.Date;
        var daysUntilTarget = ((int)targetDay - (int)current.DayOfWeek + 7) % 7;

        // If it's 0 (today is the target day), move to next week
        if (daysUntilTarget == 0)
        {
            daysUntilTarget = 7;
        }

        return current.AddDays(daysUntilTarget);
    }

    private static DateTime GetLastDayOfWeek(DateTime reference, DayOfWeek targetDay)
    {
        var current = reference.Date;
        var daysSinceTarget = ((int)current.DayOfWeek - (int)targetDay + 7) % 7;

        // If it's 0 (today is the target day), move to last week
        if (daysSinceTarget == 0)
        {
            daysSinceTarget = 7;
        }

        return current.AddDays(-daysSinceTarget);
    }
}
