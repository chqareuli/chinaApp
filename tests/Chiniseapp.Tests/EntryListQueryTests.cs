using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;

namespace Chiniseapp.Tests;

public class EntryListQueryTests
{
    [Theory]
    [InlineData("中", true)]
    [InlineData("ქა", true)]
    [InlineData("ab", true)]
    [InlineData("a1", false)]
    [InlineData("12", false)]
    [InlineData("ქ", false)]
    public void SearchRequiresHanOrTwoLetters(string text, bool expected) =>
        Assert.Equal(expected, EntryListQuery.CanSearchContent(text));

    private static readonly Entry[] Entries = [
        new() { Id = 1, Lemma = "甲", SearchNormalizedTitle = "甲", Status = EntryStatus.NewEntry, CreatedByEditorId = 7, MainAuthorEditorId = 8, LastEditorEditorId = 9 },
        new() { Id = 2, Lemma = "乙", SearchNormalizedTitle = "乙", Status = EntryStatus.KaReview, MainAuthorEditorId = 8, LastEditorEditorId = 9 },
        new() { Id = 3, Lemma = "丙", SearchNormalizedTitle = "丙", Status = EntryStatus.Published, MainAuthorEditorId = 7, LastEditorEditorId = 8 },
    ];

    private static Entry[] Find(EntryListFilter filter, EditorRole role = EditorRole.Editor) =>
        EntryListQuery.Filter(Entries.AsQueryable(), Array.Empty<Segment>().AsQueryable(), filter, role).ToArray();

    [Fact]
    public void ExplicitStageCannotExpandGeorgianEditorVisibility() =>
        Assert.Empty(Find(new(Status: EntryStatus.Published), EditorRole.KaEditor));

    [Fact]
    public void GeorgianEditorOnlySeesReviewEntries() =>
        Assert.Equal([2], Find(new(), EditorRole.KaEditor).Select(e => e.Id));

    [Fact]
    public void HomeExcludesDraftsWithoutHidingThemFromAllEntries()
    {
        Assert.Equal([2, 3], Find(new(ExcludeDrafts: true)).Select(e => e.Id));
        Assert.Equal([1, 2, 3], Find(new()).Select(e => e.Id));
    }

    [Fact]
    public void AuthorFilterUsesMainAuthorNotCreator() =>
        Assert.Equal([3], Find(new(AuthorId: 7)).Select(e => e.Id));

    [Fact]
    public void AuthorEditorAndStageFiltersIntersect() =>
        Assert.Equal([2], Find(new(AuthorId: 8, EditorId: 9, Status: EntryStatus.KaReview)).Select(e => e.Id));

    [Fact]
    public void FullTextSearchFindsDefinitionWithoutMatchingHeadword()
    {
        var segments = new[] { new Segment { EntryId = 3, Value = "სატესტო განმარტება" } }.AsQueryable();
        var found = EntryListQuery.Filter(Entries.AsQueryable(), segments, new(Query: "განმარტება"), EditorRole.Editor);
        Assert.Equal([3], found.Select(e => e.Id));
    }

    [Fact]
    public void SortingHasStableTieBreakerAcrossPages()
    {
        var sorted = EntryListQuery.Sort(Entries.Reverse().AsQueryable(), "UpdatedDesc");
        Assert.Equal([1, 2], sorted.Take(2).Select(e => e.Id));
        Assert.Equal([3], sorted.Skip(2).Take(2).Select(e => e.Id));
    }
}
