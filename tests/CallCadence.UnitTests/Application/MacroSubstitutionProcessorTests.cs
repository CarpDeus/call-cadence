using System.Globalization;
using CallCadence.Infrastructure.ApiCall;
using FluentAssertions;
using NUnit.Framework;

namespace CallCadence.UnitTests.Application;

[TestFixture]
public sealed class MacroSubstitutionProcessorTests
{
    [Test]
    public void Process_ShouldSubstituteSupportedDateTimeMacro()
    {
        // Arrange
        var input = "@@yyyy@@-@@MMM@@-@@dd@@";
        var expected = DateTime.UtcNow.ToString("yyyy-MMM-dd", CultureInfo.InvariantCulture);

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        result.Should().Be(expected);
    }

    [Test]
    public void Process_ShouldLeaveUnsupportedMacroUnchanged()
    {
        // Arrange
        var input = "prefix-@@NotSupported@@-suffix";

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        result.Should().Be(input);
    }

    [Test]
    public void Process_ShouldSubstituteEvalMacro_WithToday()
    {
        // Arrange
        var input = "@@eval:today@@";
        var expected = DateTime.UtcNow.Date.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        result.Should().Be(expected);
    }

    [Test]
    public void Process_ShouldSubstituteEvalMacro_WithDateArithmetic()
    {
        // Arrange
        var input = "@@eval:today - 5 days@@";
        var expected = DateTime.UtcNow.Date.AddDays(-5).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        result.Should().Be(expected);
    }

    [Test]
    public void Process_ShouldSubstituteEvalMacro_WithCustomFormat()
    {
        // Arrange
        var input = "@@eval:today - 5 days:yyyy-MM-dd@@";
        var expected = DateTime.UtcNow.Date.AddDays(-5).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        result.Should().Be(expected);
    }

    [Test]
    public void Process_ShouldSubstituteEvalMacro_WithNextMonday()
    {
        // Arrange
        var input = "@@eval:nextMonday@@";

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        var parsedDate = DateTime.Parse(result, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        parsedDate.DayOfWeek.Should().Be(DayOfWeek.Monday);
        parsedDate.Should().BeAfter(DateTime.Now.Date);
    }

    [Test]
    public void Process_ShouldSubstituteEvalMacro_WithNumericExpression()
    {
        // Arrange
        var input = "@@eval:100 - 25@@";

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        result.Should().Be("75");
    }

    [Test]
    public void Process_ShouldHandleMixedMacros_DateTimeAndEval()
    {
        // Arrange
        var input = "Date: @@yyyy@@-@@MM@@-@@dd@@, Eval: @@eval:today - 1 day:yyyy-MM-dd@@";
        var dateTimePart = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var evalPart = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var expected = $"Date: {dateTimePart}, Eval: {evalPart}";

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        result.Should().Be(expected);
    }

    [Test]
    public void Process_ShouldLeaveInvalidEvalMacroUnchanged()
    {
        // Arrange
        var input = "@@eval:unknownKeyword@@";

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        result.Should().Be(input);
    }

    [Test]
    public void Process_ShouldLeaveEmptyEvalExpressionUnchanged()
    {
        // Arrange
        var input = "@@eval:@@";

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        result.Should().Be(input);
    }

    [Test]
    public void Process_ShouldHandleMultipleEvalMacros()
    {
        // Arrange
        var input = "Start: @@eval:today:yyyy-MM-dd@@, End: @@eval:today + 7 days:yyyy-MM-dd@@";
        var startDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endDate = DateTime.UtcNow.Date.AddDays(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var expected = $"Start: {startDate}, End: {endDate}";

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        result.Should().Be(expected);
    }

    [Test]
    public void Process_ShouldHandleEvalMacroInUrl()
    {
        // Arrange
        var input = "https://api.example.com/data?date=@@eval:today - 1 day:yyyy-MM-dd@@";
        var date = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var expected = $"https://api.example.com/data?date={date}";

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        result.Should().Be(expected);
    }

    [Test]
    public void Process_ShouldPreserveTextWithoutMacros()
    {
        // Arrange
        var input = "Plain text without any macros";

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        result.Should().Be(input);
    }

    [Test]
    public void Process_ShouldHandleEvalMacroWithComplexDateExpression()
    {
        // Arrange
        var input = "@@eval:lastFriday + 2 weeks:MMM dd, yyyy@@";
        var today = DateTime.UtcNow.Date;
        var daysSinceFriday = ((int)today.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
        if (daysSinceFriday == 0) daysSinceFriday = 7;
        var lastFriday = today.AddDays(-daysSinceFriday);
        var expected = lastFriday.AddDays(14).ToString("MMM dd, yyyy", CultureInfo.InvariantCulture);

        // Act
        var result = MacroSubstitutionProcessor.Process(input);

        // Assert
        result.Should().Be(expected);
    }
}

