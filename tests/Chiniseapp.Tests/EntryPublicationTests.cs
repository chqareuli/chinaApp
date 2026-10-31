using Chiniseapp.Application.Entries;
using Chiniseapp.Infrastructure.Entries;

namespace Chiniseapp.Tests;

public class EntryPublicationTests
{
    private static SaveEntryContentRequest Content(SenseDto sense) => new(null, null, [], 0) { Senses = [sense] };

    [Fact]
    public void MissingPronunciationCannotPublishEvenWithCompleteDefinition() =>
        Assert.Throws<EntryValidationException>(() => EntryPublicationValidator.Validate(Content(new(1, "definition", [])), new([], [])));

    [Fact]
    public void EmptyDefinitionWithValidReferenceCanPublish()
    {
        var sense = new SenseDto(1, "", []) { CrossReferences = [new(1, "equivalent", "იგივეა", "测试", 9, null, null, "Attached")] };
        EntryPublicationValidator.Validate(Content(sense), new([new(1, "cè", 0)], []));
        Assert.Throws<EntryValidationException>(() => EntryPublicationValidator.Validate(Content(sense with { CrossReferences = [] }), new([new(1, "cè", 0)], [])));
        Assert.Throws<EntryValidationException>(() => EntryPublicationValidator.Validate(Content(sense with {
            CrossReferences = [new(1, "equivalent", "იგივეა", "", null, null, null, "Attached")] }), new([new(1, "cè", 0)], [])));
    }

    [Fact]
    public void ExampleAloneDoesNotCountAsDefinition() =>
        Assert.Throws<EntryValidationException>(() => EntryPublicationValidator.Validate(Content(new(1, "", [new("你好", "გამარჯობა")])), new([new(1, "cè", 0)], [])));

    [Fact]
    public void SixPronunciationsAreValidButSevenAndDuplicatesAreRejected()
    {
        var forms = new LexicalForms(Enumerable.Range(1, 6).Select(i => new FormItemDto(i, $"pinyin{i}", i - 1)).ToList(), []);
        var content = Content(new(1, "definition", []));
        EntryPublicationValidator.Validate(content, forms);
        Assert.Throws<EntryValidationException>(() => EntryPublicationValidator.Validate(content, forms with { Pinyins = [.. forms.Pinyins, new(7, "seventh", 6)] }));
        Assert.Throws<EntryValidationException>(() => EntryPublicationValidator.Validate(content, new([new(1, "cè", 0), new(2, "CÈ", 1)], [])));
    }
}
