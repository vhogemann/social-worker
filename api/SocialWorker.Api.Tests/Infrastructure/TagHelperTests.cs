using System;
using System.Collections.Generic;
using SocialWorker.Api.Infrastructure.Tags;
using Xunit;

namespace SocialWorker.Api.Tests.Infrastructure;

public sealed class TagHelperTests
{
    [Fact]
    public void NormalizeTags_Trims_Lowers_AndDeduplicates()
    {
        var input = new[] { " Default ", "default", "TECH", "  news  ", "", "   " };
        var result = TagHelper.NormalizeTags(input);

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { "default", "tech", "news" }, result);
    }

    [Fact]
    public void HasTag_MatchesCaseInsensitively()
    {
        var tags = new[] { "default", "personal" };

        Assert.True(TagHelper.HasTag(tags, "DEFAULT"));
        Assert.True(TagHelper.HasTag(tags, " default "));
        Assert.False(TagHelper.HasTag(tags, "work"));
        Assert.False(TagHelper.HasTag(null, "default"));
    }

    [Fact]
    public void AddTag_AppendsNewNormalizedTag()
    {
        var tags = new List<string> { "tech" };
        var updated = TagHelper.AddTag(tags, "Default");

        Assert.Contains("default", updated);
        Assert.Contains("tech", updated);
        Assert.Equal(2, updated.Count);

        // Does not duplicate
        var unchanged = TagHelper.AddTag(updated, "default");
        Assert.Equal(2, unchanged.Count);
    }

    [Fact]
    public void RemoveTag_RemovesTargetTag()
    {
        var tags = new List<string> { "tech", "default", "news" };
        var updated = TagHelper.RemoveTag(tags, "DEFAULT");

        Assert.DoesNotContain("default", updated);
        Assert.Contains("tech", updated);
        Assert.Contains("news", updated);
    }

    [Fact]
    public void ApplyUpdate_HandlesActionAndDirectCollections()
    {
        var existing = new List<string> { "initial" };

        // action: add
        var added = TagHelper.ApplyUpdate(existing, new UpdateTagsRequest(Action: "add", Tags: new List<string> { "default" }));
        Assert.Contains("default", added);
        Assert.Contains("initial", added);

        // action: remove
        var removed = TagHelper.ApplyUpdate(added, new UpdateTagsRequest(Action: "remove", Tags: new List<string> { "initial" }));
        Assert.Equal(new[] { "default" }, removed);

        // action: set
        var set = TagHelper.ApplyUpdate(removed, new UpdateTagsRequest(Action: "set", Tags: new List<string> { "new-tag" }));
        Assert.Equal(new[] { "new-tag" }, set);
    }
}
