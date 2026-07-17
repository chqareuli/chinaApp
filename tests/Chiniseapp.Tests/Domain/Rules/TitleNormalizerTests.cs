using Chiniseapp.Domain.Rules;
using Xunit;

namespace Chiniseapp.Tests.Domain.Rules;

public class TitleNormalizerTests
{
    [Fact]
    public void Normalize_folds_chinese_comma_to_regular_comma()
    {
        Assert.Equal("和尚打伞,无法无天", TitleNormalizer.Normalize("和尚打伞，无法无天"));
    }

    [Fact]
    public void Normalize_leaves_regular_comma_untouched()
    {
        Assert.Equal("和尚打伞,无法无天", TitleNormalizer.Normalize("和尚打伞,无法无天"));
    }

    [Fact]
    public void Normalize_leaves_titles_without_commas_untouched()
    {
        Assert.Equal("为", TitleNormalizer.Normalize("为"));
    }
}
