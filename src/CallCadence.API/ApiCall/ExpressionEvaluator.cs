using System.Globalization;
using System.Text.RegularExpressions;

namespace CallCadence.Infrastructure.ApiCall;

/// <summary>
/// Evaluates mathematical expressions on dates and numbers.
/// </summary>
public static partial class ExpressionEvaluator
{
    private const string DefaultDateFormat = "yyyy-MM-ddTHH:mm:ssZ";

    private static readonly HashSet<string> DateUnits =
    [
        "day", "days",
        "hour", "hours",
        "minute", "minutes",
        "month", "months",
        "year", "years",
        "week", "weeks"
    ];

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    /// Evaluates an expression and returns the result as a formatted string.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <param name="format">Optional format string for dates (defaults to ISO 8601).</param>
    /// <returns>An evaluation result containing the formatted value or error message.</returns>
    public static EvaluationResult Evaluate(string expression, string? format = null)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return EvaluationResult.Fail("Expression is empty");
        }

        try
        {
            var tokens = Tokenize(expression);
            if (tokens.Count == 0)
            {
                return EvaluationResult.Fail("No valid tokens found");
            }

            var evaluationResult = EvaluateTokens(tokens);
            if (evaluationResult == null)
            {
                return EvaluationResult.Fail("Evaluation returned null");
            }

            var formattedValue = ApplyFormat(evaluationResult, format);
            return EvaluationResult.Ok(formattedValue);
        }
        catch (Exception ex)
        {
            return EvaluationResult.Fail($"Evaluation error: {ex.Message}");
        }
    }

    private static List<ExpressionToken> Tokenize(string expression)
    {
        var tokens = new List<ExpressionToken>();

        // Normalize whitespace
        var normalized = WhitespaceRegex().Replace(expression.Trim(), " ");

        // Split on spaces and operators while keeping operators
        var parts = new List<string>();
        var currentPart = string.Empty;

        foreach (var ch in normalized)
        {
            if (ch == '+' || ch == '-')
            {
                if (!string.IsNullOrEmpty(currentPart))
                {
                    parts.Add(currentPart);
                    currentPart = string.Empty;
                }
                parts.Add(ch.ToString());
            }
            else if (ch == ' ')
            {
                if (!string.IsNullOrEmpty(currentPart))
                {
                    parts.Add(currentPart);
                    currentPart = string.Empty;
                }
            }
            else
            {
                currentPart += ch;
            }
        }

        if (!string.IsNullOrEmpty(currentPart))
        {
            parts.Add(currentPart);
        }

        // Classify tokens
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];

            if (part is "+" or "-")
            {
                tokens.Add(ExpressionToken.Operator(part));
            }
            else if (int.TryParse(part, out _) || decimal.TryParse(part, out _))
            {
                tokens.Add(ExpressionToken.Number(part));
            }
            else if (DateUnits.Contains(part.ToLowerInvariant()))
            {
                tokens.Add(ExpressionToken.Unit(part.ToLowerInvariant()));
            }
            else
            {
                // Treat as keyword (date keyword or unknown)
                tokens.Add(ExpressionToken.Keyword(part.ToLowerInvariant()));
            }
        }

        return tokens;
    }

    private static object? EvaluateTokens(List<ExpressionToken> tokens)
    {
        if (tokens.Count == 0)
        {
            return null;
        }

        // Single token - could be a keyword or number
        if (tokens.Count == 1)
        {
            var token = tokens[0];
            if (token.Type == TokenType.Keyword)
            {
                return DateKeywordResolver.TryResolveKeyword(token.Value, out var date) 
                    ? date 
                    : null;
            }
            if (token.Type == TokenType.Number)
            {
                return decimal.TryParse(token.Value, out var num) ? num : null;
            }
            return null;
        }

        // Multi-token expression - evaluate left-to-right
        object? accumulator = null;
        string? pendingOperator = null;
        decimal? pendingNumber = null;
        string? pendingUnit = null;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            switch (token.Type)
            {
                case TokenType.Keyword:
                    if (DateKeywordResolver.TryResolveKeyword(token.Value, out var resolvedDate))
                    {
                        accumulator = resolvedDate;
                    }
                    else
                    {
                        return null; // Unknown keyword
                    }
                    break;

                case TokenType.Number:
                    if (accumulator == null)
                    {
                        // Starting with a number
                        accumulator = decimal.Parse(token.Value);
                    }
                    else
                    {
                        // Number as part of date arithmetic
                        pendingNumber = decimal.Parse(token.Value);
                    }
                    break;

                case TokenType.Operator:
                    pendingOperator = token.Value;
                    break;

                case TokenType.Unit:
                    pendingUnit = token.Value;

                    // Apply date arithmetic when we have operator, number, and unit
                    if (accumulator is DateTime dateValue && pendingOperator != null && pendingNumber.HasValue)
                    {
                        accumulator = ApplyDateArithmetic(dateValue, pendingOperator, pendingNumber.Value, pendingUnit);
                        pendingOperator = null;
                        pendingNumber = null;
                        pendingUnit = null;
                    }
                    break;
            }
        }

        // Handle numeric arithmetic (no units involved)
        if (accumulator is decimal decValue && pendingOperator != null && pendingNumber.HasValue && pendingUnit == null)
        {
            accumulator = pendingOperator switch
            {
                "+" => decValue + pendingNumber.Value,
                "-" => decValue - pendingNumber.Value,
                _ => accumulator
            };
        }

        return accumulator;
    }

    private static DateTime ApplyDateArithmetic(DateTime date, string op, decimal value, string unit)
    {
        var intValue = (int)value;
        var isAddition = op == "+";
        var amount = isAddition ? intValue : -intValue;

        return unit switch
        {
            "day" or "days" => date.AddDays(amount),
            "hour" or "hours" => date.AddHours(amount),
            "minute" or "minutes" => date.AddMinutes(amount),
            "week" or "weeks" => date.AddDays(amount * 7),
            "month" or "months" => date.AddMonths(amount),
            "year" or "years" => date.AddYears(amount),
            _ => date
        };
    }

    private static string ApplyFormat(object result, string? format)
    {
        return result switch
        {
            DateTime dateTime => FormatDateTime(dateTime, format),
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            int intValue => intValue.ToString(CultureInfo.InvariantCulture),
            _ => result.ToString() ?? string.Empty
        };
    }

    private static string FormatDateTime(DateTime dateTime, string? format)
    {
        var formatString = string.IsNullOrWhiteSpace(format) ? DefaultDateFormat : format;

        try
        {
            return dateTime.ToString(formatString, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            // If custom format is invalid, fall back to default
            return dateTime.ToString(DefaultDateFormat, CultureInfo.InvariantCulture);
        }
    }
}
