using System.Text.RegularExpressions;

namespace AutoAnki.Core;

public static partial class SelectionNormalizer
{
    public const int MaxCharacters = 120;
    public const int MaxWords = 12;

    public static SelectionCaptureResult Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return SelectionCaptureResult.Failure(SelectionCaptureError.NoSelection);
        }

        var text = WhitespaceRegex().Replace(input, " ").Trim();
        text = RemoveWrappingQuotes(text);
        text = text.TrimEnd('.', ',', ';', ':', '!', '?').Trim();

        if (string.IsNullOrWhiteSpace(text))
        {
            return SelectionCaptureResult.Failure(SelectionCaptureError.NoSelection);
        }

        if (text.Length > MaxCharacters)
        {
            return SelectionCaptureResult.Failure(SelectionCaptureError.TooLong);
        }

        if (text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > MaxWords)
        {
            return SelectionCaptureResult.Failure(SelectionCaptureError.TooManyWords);
        }

        return SelectionCaptureResult.Success(text);
    }

    private static string RemoveWrappingQuotes(string value)
    {
        while (value.Length >= 2 && IsQuotePair(value[0], value[^1]))
        {
            value = value[1..^1].Trim();
        }

        return value;
    }

    private static bool IsQuotePair(char first, char last) =>
        (first, last) is ('"', '"') or ('\'', '\'') or ('“', '”') or ('‘', '’');

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
