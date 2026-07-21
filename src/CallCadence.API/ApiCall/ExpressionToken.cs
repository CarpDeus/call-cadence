namespace CallCadence.Infrastructure.ApiCall;

/// <summary>
/// Represents a token in a parsed expression.
/// </summary>
public sealed record ExpressionToken
{
    /// <summary>
    /// Gets the type of token.
    /// </summary>
    public required TokenType Type { get; init; }

    /// <summary>
    /// Gets the string value of the token.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Creates a keyword token.
    /// </summary>
    public static ExpressionToken Keyword(string value) => new()
    {
        Type = TokenType.Keyword,
        Value = value
    };

    /// <summary>
    /// Creates a number token.
    /// </summary>
    public static ExpressionToken Number(string value) => new()
    {
        Type = TokenType.Number,
        Value = value
    };

    /// <summary>
    /// Creates an operator token.
    /// </summary>
    public static ExpressionToken Operator(string value) => new()
    {
        Type = TokenType.Operator,
        Value = value
    };

    /// <summary>
    /// Creates a unit token.
    /// </summary>
    public static ExpressionToken Unit(string value) => new()
    {
        Type = TokenType.Unit,
        Value = value
    };
}

/// <summary>
/// Defines the types of tokens in an expression.
/// </summary>
public enum TokenType
{
    /// <summary>
    /// A date keyword (e.g., today, nextMonday).
    /// </summary>
    Keyword,

    /// <summary>
    /// A numeric value.
    /// </summary>
    Number,

    /// <summary>
    /// An operator (+ or -).
    /// </summary>
    Operator,

    /// <summary>
    /// A time unit (e.g., days, hours, minutes).
    /// </summary>
    Unit
}
