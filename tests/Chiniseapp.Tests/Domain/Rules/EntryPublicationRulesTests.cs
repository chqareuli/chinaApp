using Chiniseapp.Domain.Rules;
using Xunit;

namespace Chiniseapp.Tests.Domain.Rules;

public class EntryPublicationRulesTests
{
    [Fact]
    public void Complete_entry_is_publishable()
    {
        var errors = EntryPublicationRules.Validate("学习", 1, 2, ["სწავლა", "ცოდნის შეძენა"]);

        Assert.Empty(errors);
    }

    [Fact]
    public void Title_only_entry_is_not_publishable()
    {
        var errors = EntryPublicationRules.Validate("学习", 0, 0, []);

        Assert.Contains(errors, error => error.Contains("sense", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_lemma_is_rejected(string? lemma)
    {
        var errors = EntryPublicationRules.Validate(lemma, 1, 1, ["სწავლა"]);

        Assert.Contains(errors, error => error.Contains("lemma", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_sense_requires_a_non_empty_definition()
    {
        var errors = EntryPublicationRules.Validate("学习", 1, 2, ["სწავლა", ""]);

        Assert.Contains(errors, error => error.Contains("definition", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Definition_count_must_match_sense_count()
    {
        var errors = EntryPublicationRules.Validate("学习", 1, 2, ["სწავლა"]);

        Assert.Contains(errors, error => error.Contains("definition", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_grammar_group_requires_exactly_one_pos()
    {
        var errors = EntryPublicationRules.Validate("学习", 1, 2, 1, 2, ["სწავლა", "ცოდნის შეძენა"]);

        Assert.Contains(errors, error => error.Contains("POS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Full_structured_entry_is_publishable()
    {
        var errors = EntryPublicationRules.Validate("学习", 1, 2, 2, 3, ["სწავლა", "შესწავლა", "ცოდნა"]);

        Assert.Empty(errors);
    }

    [Fact]
    public void Direct_sense_entry_is_publishable()
    {
        var errors = EntryPublicationRules.Validate("学习", 0, 0, 0, 1, ["სწავლა"]);

        Assert.Empty(errors);
    }

    [Fact]
    public void Direct_pos_entry_is_publishable()
    {
        var errors = EntryPublicationRules.Validate("学习", 0, 0, 1, 1, ["სწავლა"]);

        Assert.Empty(errors);
    }
}
