using System.Globalization;
using System.Text.RegularExpressions;

namespace CallCadence.Infrastructure.ApiCall;

public static partial class MacroSubstitutionProcessor
{
    private static readonly HashSet<string> SupportedDateTimeTokens =
    [
        "d", "dd", "ddd", "dddd",
        "f", "ff", "fff", "ffff", "fffff", "ffffff", "fffffff",
        "F", "FF", "FFF", "FFFF", "FFFFF", "FFFFFF", "FFFFFFF",
        "g", "gg",
        "h", "hh",
        "H", "HH",
        "K",
        "m", "mm",
        "M", "MM", "MMM", "MMMM",
        "s", "ss",
        "t", "tt",
        "y", "yy", "yyy", "yyyy", "yyyyy",
        "z", "zz", "zzz"
    ];

    [GeneratedRegex("@@(?<token>[^@]+)@@", RegexOptions.CultureInvariant)]
    private static partial Regex MacroRegex();

    [GeneratedRegex(@"@@eval:(?<expr>[^:@]+)(?::(?<fmt>[^@]+))?@@", RegexOptions.CultureInvariant)]
    private static partial Regex EvalMacroRegex();

    public static string Process(string input)
    {
        // First, process eval macros
        var result = EvalMacroRegex().Replace(input, match =>
        {
            var expression = match.Groups["expr"].Value;
            var format = match.Groups["fmt"].Success ? match.Groups["fmt"].Value : null;

            var evaluationResult = ExpressionEvaluator.Evaluate(expression, format);

            // If evaluation fails, return the original macro unchanged (fail-safe)
            return evaluationResult.Success ? evaluationResult.Value! : match.Value;
        });

        // Then, process traditional DateTime format macros
        result = MacroRegex().Replace(result, match =>
        {
            var token = match.Groups["token"].Value;
            if (!IsSupportedToken(token))
            {
                return match.Value;
            }

            return DateTime.Now.ToString(token, CultureInfo.InvariantCulture);
        });

        return result;
    }

    private static bool IsSupportedToken(string token)
    {
        return SupportedDateTimeTokens.Contains(token);
    }
}
