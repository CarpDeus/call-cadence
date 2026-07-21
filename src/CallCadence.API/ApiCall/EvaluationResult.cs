namespace CallCadence.Infrastructure.ApiCall;

/// <summary>
/// Represents the result of an expression evaluation.
/// </summary>
public sealed record EvaluationResult
{
    /// <summary>
    /// Gets a value indicating whether the evaluation was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the evaluated value as a string, or null if evaluation failed.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// Gets the error message if evaluation failed, or null if successful.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful evaluation result.
    /// </summary>
    public static EvaluationResult Ok(string value) => new()
    {
        Success = true,
        Value = value
    };

    /// <summary>
    /// Creates a failed evaluation result.
    /// </summary>
    public static EvaluationResult Fail(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
