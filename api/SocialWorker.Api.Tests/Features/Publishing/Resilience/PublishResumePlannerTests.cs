using System;
using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Resilience;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Resilience;

public sealed class PublishResumePlannerTests
{
    [Fact]
    public void CreatePlan_NoExistingPosts_ReturnsZeroResumeIndex()
    {
        var segments = new[] { "Segment 1", "Segment 2" };
        var existing = new List<Post>();

        var plan = PublishResumePlanner.CreatePlan(segments, existing, "Bluesky");

        Assert.True(plan.CanResume);
        Assert.Equal(0, plan.ResumeFromSegmentIndex);
        Assert.False(plan.IsFullyPublished);
        Assert.Null(plan.ErrorMessage);
    }

    [Fact]
    public void CreatePlan_MatchingPrefix_ReturnsResumeIndex()
    {
        var segments = new[] { "Segment 1", "Segment 2", "Segment 3" };
        var existing = new List<Post>
        {
            new() { SegmentIndex = 0, PublishedText = "Segment 1", RemoteId = "uri1", RemoteCid = "cid1" }
        };

        var plan = PublishResumePlanner.CreatePlan(segments, existing, "Bluesky");

        Assert.True(plan.CanResume);
        Assert.Equal(1, plan.ResumeFromSegmentIndex);
        Assert.False(plan.IsFullyPublished);
        Assert.Null(plan.ErrorMessage);
    }

    [Fact]
    public void CreatePlan_AllSegmentsPublished_ReturnsFullyPublished()
    {
        var segments = new[] { "Segment 1", "Segment 2" };
        var existing = new List<Post>
        {
            new() { SegmentIndex = 0, PublishedText = "Segment 1", RemoteId = "uri1" },
            new() { SegmentIndex = 1, PublishedText = "Segment 2", RemoteId = "uri2" }
        };

        var plan = PublishResumePlanner.CreatePlan(segments, existing, "Bluesky");

        Assert.True(plan.CanResume);
        Assert.Equal(2, plan.ResumeFromSegmentIndex);
        Assert.True(plan.IsFullyPublished);
        Assert.Null(plan.ErrorMessage);
    }

    [Fact]
    public void CreatePlan_ModifiedPrefix_ReturnsCannotResume()
    {
        var segments = new[] { "Segment 1 MODIFIED", "Segment 2" };
        var existing = new List<Post>
        {
            new() { SegmentIndex = 0, PublishedText = "Segment 1", RemoteId = "uri1" }
        };

        var plan = PublishResumePlanner.CreatePlan(segments, existing, "Bluesky");

        Assert.False(plan.CanResume);
        Assert.NotNull(plan.ErrorMessage);
        Assert.Contains("segment 1 was previously published with different content", plan.ErrorMessage);
        Assert.Contains("Bluesky", plan.ErrorMessage);
    }

    [Fact]
    public void CreatePlan_FewerSegmentsThanPublished_ReturnsCannotResume()
    {
        var segments = new[] { "Segment 1" };
        var existing = new List<Post>
        {
            new() { SegmentIndex = 0, PublishedText = "Segment 1" },
            new() { SegmentIndex = 1, PublishedText = "Segment 2" }
        };

        var plan = PublishResumePlanner.CreatePlan(segments, existing, "Bluesky");

        Assert.False(plan.CanResume);
        Assert.NotNull(plan.ErrorMessage);
        Assert.Contains("fewer segments", plan.ErrorMessage);
    }

    [Fact]
    public void CreatePlan_IndexGapInExistingPosts_ReturnsCannotResume()
    {
        var segments = new[] { "Segment 1", "Segment 2", "Segment 3" };
        var existing = new List<Post>
        {
            new() { SegmentIndex = 0, PublishedText = "Segment 1" },
            new() { SegmentIndex = 2, PublishedText = "Segment 3" }
        };

        var plan = PublishResumePlanner.CreatePlan(segments, existing, "Bluesky");

        Assert.False(plan.CanResume);
        Assert.NotNull(plan.ErrorMessage);
        Assert.Contains("gap or mismatch", plan.ErrorMessage);
    }
}
