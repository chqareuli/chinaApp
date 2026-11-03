using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Infrastructure.Entries;

namespace Chiniseapp.Tests;

public class EntryMediaRulesTests
{
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
