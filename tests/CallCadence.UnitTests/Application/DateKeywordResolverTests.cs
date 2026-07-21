using CallCadence.Infrastructure.ApiCall;
using FluentAssertions;
using NUnit.Framework;

namespace CallCadence.UnitTests.Application;

[TestFixture]
public sealed class DateKeywordResolverTests
{
    [Test]
    public void TryResolveKeyword_ShouldResolveToday()
    {
        // Act
        var result = DateKeywordResolver.TryResolveKeyword("today", out var date);

        // Assert
        result.Should().BeTrue();
        date.Should().Be(DateTime.UtcNow.Date);
    }

    [Test]
    public void TryResolveKeyword_ShouldResolveNow()
    {
        // Act
        var beforeResolve = DateTime.UtcNow;
        var result = DateKeywordResolver.TryResolveKeyword("now", out var date);
        var afterResolve = DateTime.UtcNow;

        // Assert
        result.Should().BeTrue();
        date.Should().BeOnOrAfter(beforeResolve).And.BeOnOrBefore(afterResolve);
    }

    [Test]
    public void TryResolveKeyword_ShouldResolveUtcNow()
    {
        // Act
        var beforeResolve = DateTime.UtcNow;
        var result = DateKeywordResolver.TryResolveKeyword("utcnow", out var date);
        var afterResolve = DateTime.UtcNow;

        // Assert
        result.Should().BeTrue();
        date.Should().BeOnOrAfter(beforeResolve).And.BeOnOrBefore(afterResolve);
    }

    [Test]
    public void TryResolveKeyword_ShouldResolveYesterday()
    {
        // Act
        var result = DateKeywordResolver.TryResolveKeyword("yesterday", out var date);

        // Assert
        result.Should().BeTrue();
        date.Should().Be(DateTime.UtcNow.Date.AddDays(-1));
    }

    [Test]
    public void TryResolveKeyword_ShouldResolveTomorrow()
    {
        // Act
        var result = DateKeywordResolver.TryResolveKeyword("tomorrow", out var date);

        // Assert
        result.Should().BeTrue();
        date.Should().Be(DateTime.UtcNow.Date.AddDays(1));
    }

    [Test]
    [TestCase("monday", DayOfWeek.Monday)]
    [TestCase("tuesday", DayOfWeek.Tuesday)]
    [TestCase("wednesday", DayOfWeek.Wednesday)]
    [TestCase("thursday", DayOfWeek.Thursday)]
    [TestCase("friday", DayOfWeek.Friday)]
    [TestCase("saturday", DayOfWeek.Saturday)]
    [TestCase("sunday", DayOfWeek.Sunday)]
    public void TryResolveKeyword_ShouldResolveDayName_ToCurrentOrNext(string dayName, DayOfWeek expectedDay)
    {
        // Act
        var result = DateKeywordResolver.TryResolveKeyword(dayName, out var date);

        // Assert
        result.Should().BeTrue();
        date.DayOfWeek.Should().Be(expectedDay);
        date.Should().BeOnOrAfter(DateTime.UtcNow.Date);
        date.Should().BeOnOrBefore(DateTime.UtcNow.Date.AddDays(7));
    }

    [Test]
    [TestCase("nextMonday", DayOfWeek.Monday)]
    [TestCase("nextTuesday", DayOfWeek.Tuesday)]
    [TestCase("nextWednesday", DayOfWeek.Wednesday)]
    [TestCase("nextThursday", DayOfWeek.Thursday)]
    [TestCase("nextFriday", DayOfWeek.Friday)]
    [TestCase("nextSaturday", DayOfWeek.Saturday)]
    [TestCase("nextSunday", DayOfWeek.Sunday)]
    public void TryResolveKeyword_ShouldResolveNextDay(string keyword, DayOfWeek expectedDay)
    {
        // Act
        var result = DateKeywordResolver.TryResolveKeyword(keyword, out var date);

        // Assert
        result.Should().BeTrue();
        date.DayOfWeek.Should().Be(expectedDay);
        date.Should().BeAfter(DateTime.UtcNow.Date); // Must be in the future
        date.Should().BeOnOrBefore(DateTime.UtcNow.Date.AddDays(7));
    }

    [Test]
    [TestCase("lastMonday", DayOfWeek.Monday)]
    [TestCase("lastTuesday", DayOfWeek.Tuesday)]
    [TestCase("lastWednesday", DayOfWeek.Wednesday)]
    [TestCase("lastThursday", DayOfWeek.Thursday)]
    [TestCase("lastFriday", DayOfWeek.Friday)]
    [TestCase("lastSaturday", DayOfWeek.Saturday)]
    [TestCase("lastSunday", DayOfWeek.Sunday)]
    public void TryResolveKeyword_ShouldResolveLastDay(string keyword, DayOfWeek expectedDay)
    {
        // Act
        var result = DateKeywordResolver.TryResolveKeyword(keyword, out var date);

        // Assert
        result.Should().BeTrue();
        date.DayOfWeek.Should().Be(expectedDay);
        date.Should().BeBefore(DateTime.UtcNow.Date); // Must be in the past
        date.Should().BeOnOrAfter(DateTime.UtcNow.Date.AddDays(-7));
    }

    [Test]
    public void TryResolveKeyword_ShouldBeCaseInsensitive()
    {
        // Act
        var resultLower = DateKeywordResolver.TryResolveKeyword("today", out var dateLower);
        var resultUpper = DateKeywordResolver.TryResolveKeyword("TODAY", out var dateUpper);
        var resultMixed = DateKeywordResolver.TryResolveKeyword("ToDay", out var dateMixed);

        // Assert
        resultLower.Should().BeTrue();
        resultUpper.Should().BeTrue();
        resultMixed.Should().BeTrue();
        dateLower.Should().Be(dateUpper);
        dateUpper.Should().Be(dateMixed);
    }

    [Test]
    public void TryResolveKeyword_ShouldReturnFalse_ForUnknownKeyword()
    {
        // Act
        var result = DateKeywordResolver.TryResolveKeyword("unknownKeyword", out var date);

        // Assert
        result.Should().BeFalse();
        date.Should().Be(DateTime.MinValue);
    }

    [Test]
    public void TryResolveKeyword_ShouldReturnFalse_ForEmptyString()
    {
        // Act
        var result = DateKeywordResolver.TryResolveKeyword("", out var date);

        // Assert
        result.Should().BeFalse();
        date.Should().Be(DateTime.MinValue);
    }

    [Test]
    public void TryResolveKeyword_ShouldReturnFalse_ForInvalidDayPrefix()
    {
        // Act
        var result = DateKeywordResolver.TryResolveKeyword("invalidMonday", out var date);

        // Assert
        result.Should().BeFalse();
        date.Should().Be(DateTime.MinValue);
    }

    [Test]
    public void TryResolveKeyword_NextDay_ShouldNotReturnToday_WhenTodayIsTargetDay()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var todayDayName = today.DayOfWeek.ToString().ToLowerInvariant();
        var nextKeyword = $"next{char.ToUpperInvariant(todayDayName[0])}{todayDayName[1..]}";

        // Act
        var result = DateKeywordResolver.TryResolveKeyword(nextKeyword, out var date);

        // Assert
        result.Should().BeTrue();
        date.Should().Be(today.AddDays(7)); // Should be next week
    }

    [Test]
    public void TryResolveKeyword_LastDay_ShouldNotReturnToday_WhenTodayIsTargetDay()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var todayDayName = today.DayOfWeek.ToString().ToLowerInvariant();
        var lastKeyword = $"last{char.ToUpperInvariant(todayDayName[0])}{todayDayName[1..]}";

        // Act
        var result = DateKeywordResolver.TryResolveKeyword(lastKeyword, out var date);

        // Assert
        result.Should().BeTrue();
        date.Should().Be(today.AddDays(-7)); // Should be last week
    }
}
