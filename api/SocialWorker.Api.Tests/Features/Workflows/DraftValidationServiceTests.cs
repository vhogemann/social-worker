using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Workflows.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Workflows;

public sealed class DraftValidationServiceTests : SqliteTestBase
{
    [Fact]
    public async Task ValidateDraft_ReturnsInvalid_WhenDraftHasNoContentOrSegments()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = Draft.Create(user.Id, "Empty Draft");
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var service = new DraftValidationService(db, NullLogger<DraftValidationService>.Instance);
        var result = await service.ValidateDraftAsync(draft, null, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("No content or segments generated.", result.Feedback);
    }

    [Fact]
    public async Task ValidateDraft_ReturnsValid_WhenDraftHasContent()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = Draft.Create(user.Id, "Valid Draft", "Here is some drafted content.");
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var service = new DraftValidationService(db, NullLogger<DraftValidationService>.Instance);
        var result = await service.ValidateDraftAsync(draft, null, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Null(result.Feedback);
    }

    [Fact]
    public async Task ValidateDraft_ReturnsInvalid_WhenBlueskySegmentExceedsLimit()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = Draft.Create(user.Id, "Long Draft");
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var segment = new ThreadSegment
        {
            DraftId = draft.Id,
            Position = 0,
            Content = new string('A', 350) // exceeds 300
        };
        db.ThreadSegments.Add(segment);
        await db.SaveChangesAsync();

        var service = new DraftValidationService(db, NullLogger<DraftValidationService>.Instance);
        var result = await service.ValidateDraftAsync(draft, "Bluesky", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("character limit", result.Feedback);
    }
}
