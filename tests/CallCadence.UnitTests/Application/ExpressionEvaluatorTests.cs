using System.Globalization;
using CallCadence.Infrastructure.ApiCall;
using FluentAssertions;
using NUnit.Framework;

namespace CallCadence.UnitTests.Application;

[TestFixture]
public sealed class ExpressionEvaluatorTests
{
    [Test]
    public void Evaluate_ShouldReturnToday_InIso8601Format()
    {
        // Arrange
        var expression = "today";
        var expectedDate = DateTime.UtcNow.Date.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldReturnNow_WithCurrentTime()
    {
        // Arrange
        var expression = "now";
        var beforeEval = DateTime.Now;

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        var parsedDate = DateTime.Parse(result.Value!);
        parsedDate.Should().BeCloseTo(beforeEval, TimeSpan.FromSeconds(1));
    }

    [Test]
    public void Evaluate_ShouldReturnYesterday()
    {
        // Arrange
        var expression = "yesterday";
        var expectedDate = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldReturnTomorrow()
    {
        // Arrange
        var expression = "tomorrow";
        var expectedDate = DateTime.UtcNow.Date.AddDays(1).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldSubtractDaysFromToday()
    {
        // Arrange
        var expression = "today - 5 days";
        var expectedDate = DateTime.UtcNow.Date.AddDays(-5).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldAddDaysToToday()
    {
        // Arrange
        var expression = "today + 10 days";
        var expectedDate = DateTime.UtcNow.Date.AddDays(10).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldSubtractHoursFromNow()
    {
        // Arrange
        var expression = "now - 2 hours";
        var expectedDate = DateTime.Now.AddHours(-2);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        var parsedDate = DateTime.Parse(result.Value!);
        parsedDate.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
    }

    [Test]
    public void Evaluate_ShouldAddMinutesToNow()
    {
        // Arrange
        var expression = "now + 30 minutes";
        var expectedDate = DateTime.Now.AddMinutes(30);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        var parsedDate = DateTime.Parse(result.Value!);
        parsedDate.Should().BeCloseTo(expectedDate, TimeSpan.FromSeconds(1));
    }

    [Test]
    public void Evaluate_ShouldSubtractMonthsFromToday()
    {
        // Arrange
        var expression = "today - 3 months";
        var expectedDate = DateTime.UtcNow.Date.AddMonths(-3).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldAddYearsToToday()
    {
        // Arrange
        var expression = "today + 2 years";
        var expectedDate = DateTime.UtcNow.Date.AddYears(2).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldAddWeeksToToday()
    {
        // Arrange
        var expression = "today + 2 weeks";
        var expectedDate = DateTime.UtcNow.Date.AddDays(14).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldHandleSingularUnit()
    {
        // Arrange
        var expression = "today - 1 day";
        var expectedDate = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldReturnNextMonday()
    {
        // Arrange
        var expression = "nextMonday";
        var today = DateTime.UtcNow.Date;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;
        var expectedDate = today.AddDays(daysUntilMonday).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldReturnLastFriday()
    {
        // Arrange
        var expression = "lastFriday";
        var today = DateTime.UtcNow.Date;
        var daysSinceFriday = ((int)today.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
        if (daysSinceFriday == 0) daysSinceFriday = 7;
        var expectedDate = today.AddDays(-daysSinceFriday).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldAddDaysToNextMonday()
    {
        // Arrange
        var expression = "nextMonday + 3 days";
        var today = DateTime.UtcNow.Date;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;
        var expectedDate = today.AddDays(daysUntilMonday + 3).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldFormatWithCustomDateFormat()
    {
        // Arrange
        var expression = "today - 5 days";
        var format = "yyyy-MM-dd";
        var expectedDate = DateTime.UtcNow.Date.AddDays(-5).ToString(format, CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression, format);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldFormatWithCustomComplexFormat()
    {
        // Arrange
        var expression = "today";
        var format = "MMM dd, yyyy";
        var expectedDate = DateTime.UtcNow.Date.ToString(format, CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression, format);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    [Test]
    public void Evaluate_ShouldHandleNumericAddition()
    {
        // Arrange
        var expression = "10 + 5";

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be("15");
    }

    [Test]
    public void Evaluate_ShouldHandleNumericSubtraction()
    {
        // Arrange
        var expression = "100 - 25";

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be("75");
    }

    [Test]
    public void Evaluate_ShouldReturnFailure_ForEmptyExpression()
    {
        // Arrange
        var expression = "";

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }

    [Test]
    public void Evaluate_ShouldReturnFailure_ForUnknownKeyword()
    {
        // Arrange
        var expression = "unknownKeyword";

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeFalse();
    }

    // TODO: Make this test work

    //[Test]
    //public void Evaluate_ShouldHandleExpressionWithoutSpaces()
    //{
    //    // Arrange
    //    var expression = "today-5days";
    //    var expectedDate = DateTime.Now.Date.AddDays(-5).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    //    // Act
    //    var result = ExpressionEvaluator.Evaluate(expression);

    //    // Assert
    // //   result.Success.Should().BeTrue();
    //    result.Value.Should().Be(expectedDate);
    //}

    [Test]
    public void Evaluate_ShouldHandleExtraWhitespace()
    {
        // Arrange
        var expression = "  today   -   5   days  ";
        var expectedDate = DateTime.UtcNow.Date.AddDays(-5).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        // Act
        var result = ExpressionEvaluator.Evaluate(expression);

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be(expectedDate);
    }

    //TODO Make this work
    //[Test]
    //public void Evaluate_ShouldFallbackToDefaultFormat_OnInvalidFormat()
    //{
    //    // Arrange
    //    var expression = "today";
    //    var invalidFormat = "invalid{format}";
    //    var expectedDate = DateTime.UtcNow.Date.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    //    // Act
    //    var result = ExpressionEvaluator.Evaluate(expression, invalidFormat);

    //    // Assert
    //    result.Success.Should().BeTrue();
    //    result.Value.Should().Be(expectedDate);
    //}
}
