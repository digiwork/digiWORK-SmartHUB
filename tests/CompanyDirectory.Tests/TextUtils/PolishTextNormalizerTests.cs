using CompanyDirectory.Shared.TextUtils;

namespace CompanyDirectory.Tests.TextUtils;

public class PolishTextNormalizerTests
{
    // ── Basic passthrough ────────────────────────────────────────────────────

    [Fact]
    public void Normalize_EmptyString_ReturnsEmpty()
        => Assert.Equal(string.Empty, PolishTextNormalizer.Normalize(string.Empty));

    [Fact]
    public void Normalize_NoPolishChars_ReturnsUnchanged()
        => Assert.Equal("Jan Kowalski", PolishTextNormalizer.Normalize("Jan Kowalski"));

    // ── Lowercase replacements ───────────────────────────────────────────────

    [Theory]
    [InlineData("ą", "a")]
    [InlineData("ć", "c")]
    [InlineData("ę", "e")]
    [InlineData("ł", "l")]
    [InlineData("ń", "n")]
    [InlineData("ó", "o")]
    [InlineData("ś", "s")]
    [InlineData("ź", "z")]
    [InlineData("ż", "z")]
    public void Normalize_SingleLowercasePolishChar_ReplacesCorrectly(string input, string expected)
        => Assert.Equal(expected, PolishTextNormalizer.Normalize(input));

    // ── Uppercase replacements ───────────────────────────────────────────────

    [Theory]
    [InlineData("Ą", "A")]
    [InlineData("Ć", "C")]
    [InlineData("Ę", "E")]
    [InlineData("Ł", "L")]
    [InlineData("Ń", "N")]
    [InlineData("Ó", "O")]
    [InlineData("Ś", "S")]
    [InlineData("Ź", "Z")]
    [InlineData("Ż", "Z")]
    public void Normalize_SingleUppercasePolishChar_ReplacesCorrectly(string input, string expected)
        => Assert.Equal(expected, PolishTextNormalizer.Normalize(input));

    // ── Real names ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Łukasz Wiśniewski",  "Lukasz Wisniewski")]
    [InlineData("Zofia Żółkowska",    "Zofia Zolkowska")]
    [InlineData("Anna Śliwińska",     "Anna Sliwinska")]
    [InlineData("Marek Ćwikła",       "Marek Cwikla")]
    [InlineData("Piotr Gąsiorowski",  "Piotr Gasiorowski")]
    [InlineData("Agnieszka Szymańska","Agnieszka Szymanska")]
    public void Normalize_PolishName_ReturnsAsciiEquivalent(string input, string expected)
        => Assert.Equal(expected, PolishTextNormalizer.Normalize(input));

    [Fact]
    public void Normalize_MixedCaseAndPolish_PreservesCase()
    {
        // Non-Polish chars keep their case; Polish chars map to the defined replacement
        var result = PolishTextNormalizer.Normalize("ŁÓDŹ");
        Assert.Equal("LODZ", result);
    }

    [Fact]
    public void Normalize_NonPolishSpecialChars_Unchanged()
    {
        const string input = "abc-123 @domain.pl";
        Assert.Equal(input, PolishTextNormalizer.Normalize(input));
    }
}
