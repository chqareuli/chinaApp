using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Infrastructure.Entries;

namespace Chiniseapp.Tests;

public class EntryMediaRulesTests
{
    [Fact]
    public void ObsoleteLinksAreRetainedAsLibraryImagesAndTheWorkingSaraLinkSurvives()
    {
        var now = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);
        MediaAsset Asset(int id, int target) => new() { Id = id, EntryId = 2, InsertedIntoSegmentId = null,
            SafeStorageName = id is 12 or 22 ? "sara-file" : $"file-{id}", StorageUrl = $"/media/{id}/content", MimeType = "image/png",
            Placement = "inline:" + System.Text.Json.JsonSerializer.Serialize(new { kind = "sense", nodeId = target, field = "definition", offset = 0 }) };
        MediaAsset[] media = [Asset(12, 138), Asset(14, 119), Asset(15, 131), Asset(16, 119), Asset(22, 811)];
        Segment[] current = [new() { Id = 811, EntryId = 2, SegmentType = Chiniseapp.Domain.Enums.SegmentType.Sense }];
        var workingPlacement = media[4].Placement;
        foreach (var asset in media.Take(4)) EntryMediaRules.DetachMissingTarget(asset, current, 1, now);
        Assert.All(media.Take(4), asset => {
            Assert.True(EntryMediaRules.IsDetached(asset));
            Assert.Contains("originalPlacement", asset.Placement);
            Assert.False(EntryMediaRules.HasMissingTarget(asset, current));
        });
        Assert.Equal(5, media.Length);
        Assert.Equal("sara-file", media[0].SafeStorageName);
        Assert.Throws<EntryValidationException>(() => EntryMediaRules.DetachMissingTarget(media[4], current, 1, now));
        Assert.Equal(workingPlacement, media[4].Placement);
        var entry = new EntryDetail(2, "标测", null, "Ready", null, "Editor", now, now, 1, [], new(true, [])) {
            Senses = [new(1, "Current definition", []) { Id = 811, DefinitionId = 814 }] };
        var json = System.Text.Json.JsonSerializer.SerializeToElement(EntryExportSerializer.DictionaryContent(entry, media),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal(22, Assert.Single(json.GetProperty("media").EnumerateArray()).GetProperty("id").GetInt32());
        Assert.Empty(json.GetProperty("exportWarnings").EnumerateArray());
        var xml = System.Xml.Linq.XDocument.Parse(EntryExportSerializer.Xml([entry], media));
        System.Xml.Linq.XNamespace tei = "http://www.tei-c.org/ns/1.0";
        Assert.Equal("media-link-22", xml.Descendants(tei + "figure").Single().Attribute(System.Xml.Linq.XNamespace.Xml + "id")!.Value);
        var archived = media[0].Placement;
        EntryMediaRules.DetachMissingTarget(media[0], current, 1, now);
        Assert.Equal(archived, media[0].Placement);
    }

    [Theory]
    [InlineData("sense:138", 138)]
    [InlineData("example:119", 119)]
    [InlineData("inline:{\"kind\":\"sense\",\"nodeId\":138}", 138)]
    [InlineData("inline:{\"kind\":\"example\",\"nodeId\":119}", 119)]
    [InlineData("cover", null)]
    [InlineData("inline:{bad", null)]
    [InlineData("inline:[]", null)]
    [InlineData(null, null)]
    public void ParentCanBeReadFromCurrentAndLegacyPlacements(string? placement, int? expected) =>
        Assert.Equal(expected, EntryMediaRules.PlacementParent(placement));

    [Theory]
    [InlineData(null, "sense:138")]
    [InlineData(null, "example:138")]
    [InlineData(null, "inline:{\"kind\":\"sense\",\"nodeId\":138}")]
    [InlineData(138, null)]
    [InlineData(138, "cover")]
    [InlineData(999, "sense:138")]
    public void DeletingAnImageParentIsBlockedWithoutChangingTheMedia(int? storedParent, string? placement)
    {
        var asset = new MediaAsset { Id = 12, EntryId = 2, InsertedIntoSegmentId = storedParent, Placement = placement, SafeStorageName = "shared-file" };
        var error = Assert.Throws<EntryValidationException>(() => EntryMediaRules.EnsureSegmentsCanBeRemoved(2, new HashSet<int> { 138 }, [asset]));
        Assert.Contains("Entry 2, media 12, segment 138", error.Message);
        Assert.Equal(placement, asset.Placement);
        Assert.Equal(storedParent, asset.InsertedIntoSegmentId);
        Assert.Equal("shared-file", asset.SafeStorageName);
    }

    [Fact]
    public void EditingMovingAndDeletingUnrelatedNodesDoesNotBlockTheSave()
    {
        MediaAsset[] media = [new() { Id = 12, EntryId = 2, InsertedIntoSegmentId = 138, Placement = "sense:138" },
            new() { Id = 13, EntryId = 3, InsertedIntoSegmentId = 119 }];
        EntryMediaRules.EnsureSegmentsCanBeRemoved(2, new HashSet<int>(), media);
        EntryMediaRules.EnsureSegmentsCanBeRemoved(2, new HashSet<int> { 119 }, media);
        EntryMediaRules.EnsureSegmentsCanBeRemoved(2, new HashSet<int> { 138 }, []);
    }
}
