using AutoAnki.Core;

namespace AutoAnki.Tests;

public sealed class SelectionNormalizerTests
{
    [Theory]
    [InlineData("  reliable  ", "reliable")]
    [InlineData("\"look up!\"", "look up")]
    [InlineData("don\'t", "don\'t")]
    [InlineData("well-known,", "well-known")]
    [InlineData("one\r\n two\tthree", "one two three")]
    [InlineData("‘useful phrase’", "useful phrase")]
    public void Normalize_CleansSupportedSelections(string input, string expected)
    {
        var result = SelectionNormalizer.Normalize(input);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Text);
    }

    [Fact]
    public void Normalize_RejectsMoreThanTwelveWords()
    {
        var result = SelectionNormalizer.Normalize(string.Join(' ', Enumerable.Repeat("word", 13)));
        Assert.Equal(SelectionCaptureError.TooManyWords, result.Error);
    }

    [Fact]
    public void Normalize_RejectsMoreThanOneHundredTwentyCharacters()
    {
        var result = SelectionNormalizer.Normalize(new string('a', 121));
        Assert.Equal(SelectionCaptureError.TooLong, result.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    public void Normalize_RejectsEmptyText(string? input)
    {
        var result = SelectionNormalizer.Normalize(input);
        Assert.Equal(SelectionCaptureError.NoSelection, result.Error);
    }
}
