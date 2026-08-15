using Chiniseapp.Domain.Rules;

namespace Chiniseapp.Tests.Domain.Rules;

public class SupportMaterialRulesTests
{
    [Theory]
    [InlineData("English", "English")]
    [InlineData(" chinese ", "Chinese")]
    [InlineData("RUSSIAN", "Russian")]
    [InlineData("other", "Other")]
    public void NormalizeLanguage_ReturnsCanonicalSupportedLanguage(string value, string expected)
    {
        Assert.Equal(expected, SupportMaterialRules.NormalizeLanguage(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Georgian")]
    [InlineData(null)]
    public void NormalizeLanguage_RejectsUnsupportedLanguage(string? value)
    {
        Assert.Null(SupportMaterialRules.NormalizeLanguage(value));
    }
}
