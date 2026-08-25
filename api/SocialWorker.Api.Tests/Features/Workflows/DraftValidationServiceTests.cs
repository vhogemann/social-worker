using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Features.Workflows.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Workflows;

public sealed class DraftValidationServiceTests : SqliteTestBase
{
    private static IDraftValidatorResolver CreateResolver()
    {
        var blueskyValidator = new BlueskyDraftValidator(
            new BlueskyContentValidator(),
            new IValidateDraftRule<BlueskySegmentValidation>[]
            {
                new BlueskyMaxCharactersRule(),
                new BlueskyMaxImagesRule(),
                new BlueskyNoMixedImagesAndYouTubeRule(),
                new BlueskyUnsupportedMarkdownRule(),
                new BlueskyPlaceholderLinkRule(),
                new BlueskyPlaceholderMediaRule(),
                new BlueskyPlaceholderUrlRule(),
                new BlueskyMissingAltTextRule(),
                new BlueskyYouTubeEmbedSyntaxRule(),
                new BlueskyTitleLikeOpenerRule()
            });

        return new DraftValidatorResolver(
            new CanonicalDraftValidator(),
            blueskyValidator,
            new LinkedInDraftValidator(),
            new PlatformContentPolicy());
    }

    [Fact]
    public async Task ValidateDraft_ReturnsInvalid_WhenDraftHasNoContentOrSegments()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = Draft.Create(user.Id, "Empty Draft");
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var service = new DraftValidationService(db, CreateResolver(), NullLogger<DraftValidationService>.Instance);
        var result = await service.ValidateDraftAsync(draft, null, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal("No content or segments generated.", result.Feedback);
    }

    [Fact]
    public async Task ValidateDraft_Canonical_ReturnsValid_WhenDraftHasLongFormContent()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = Draft.Create(user.Id, "Valid Draft", $"# Canonical Draft\n\n{new string('A', 800)}\n\n---\n\n{new string('B', 600)}");
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var service = new DraftValidationService(db, CreateResolver(), NullLogger<DraftValidationService>.Instance);
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

        var service = new DraftValidationService(db, CreateResolver(), NullLogger<DraftValidationService>.Instance);
        var result = await service.ValidateDraftAsync(draft, "Bluesky", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("300", result.Feedback);
    }

    [Fact]
    public async Task ValidateDraft_LinkedIn_AllowsLongContent_ButRejectsMultiSegment()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var singlePostDraft = Draft.Create(user.Id, "LinkedIn Single", new string('A', 2000));
        var multiPostDraft = Draft.Create(user.Id, "LinkedIn Multi", "Segment 1\n\n---\n\nSegment 2");
        db.Drafts.AddRange(singlePostDraft, multiPostDraft);
        await db.SaveChangesAsync();

        var service = new DraftValidationService(db, CreateResolver(), NullLogger<DraftValidationService>.Instance);
        
        var singleResult = await service.ValidateDraftAsync(singlePostDraft, "LinkedIn", CancellationToken.None);
        Assert.True(singleResult.IsValid);

        var multiResult = await service.ValidateDraftAsync(multiPostDraft, "LinkedIn", CancellationToken.None);
        Assert.False(multiResult.IsValid);
        Assert.Contains("multi-post threads", multiResult.Feedback);
    }
}
