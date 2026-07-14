using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Sources;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class DraftTests : SqliteTestBase
{
    private readonly AppDbContext _db;
    private readonly AppUser _user;

    public DraftTests()
    {
        _db = CreateDbContext();
        _user = CreateSeedUser(_db);
    }

    protected override void Cleanup()
    {
        _db.Dispose();
        base.Cleanup();
    }

    [Fact]
    public void SplitMarkdownIntoSegments_HandlesVaryingInputs()
    {
        // Empty string fallback
        var empty = DraftsEndpoint.SplitMarkdownIntoSegments("");
        Assert.Single(empty);
        Assert.Equal("", empty[0]);

        // Single segment
        var single = DraftsEndpoint.SplitMarkdownIntoSegments("This is a post.");
        Assert.Single(single);
        Assert.Equal("This is a post.", single[0]);

        // Multi segments divided by ---
        var multi = DraftsEndpoint.SplitMarkdownIntoSegments("Post 1\n---\nPost 2\n---\nPost 3");
        Assert.Equal(3, multi.Count);
        Assert.Equal("Post 1", multi[0]);
        Assert.Equal("Post 2", multi[1]);
        Assert.Equal("Post 3", multi[2]);

        // Trims segments
        var untrimmed = DraftsEndpoint.SplitMarkdownIntoSegments("\n  Post 1  \n---\n  Post 2\n");
        Assert.Equal(2, untrimmed.Count);
        Assert.Equal("Post 1", untrimmed[0]);
        Assert.Equal("Post 2", untrimmed[1]);
    }

    [Fact]
    public async Task ReconcileSegmentsAsync_SyncsDatabaseCorrectly()
    {
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = "Test Draft",
            UserId = _user.Id,
            Content = "First segment\n---\nSecond segment\n---\nThird segment"
        };
        _db.Drafts.Add(draft);
        await _db.SaveChangesAsync();

        // 1. Initial reconciliation
        await new DraftsService(_db, null!, null!, null!, null!).ReconcileSegmentsAsync(draft, draft.Content);
        await _db.SaveChangesAsync();

        var segments = await _db.ThreadSegments
            .Where(s => s.DraftId == draft.Id)
            .OrderBy(s => s.Position)
            .ToListAsync();

        Assert.Equal(3, segments.Count);
        Assert.Equal(0, segments[0].Position);
        Assert.Equal("First segment", segments[0].Content);
        Assert.Equal(1, segments[1].Position);
        Assert.Equal("Second segment", segments[1].Content);
        Assert.Equal(2, segments[2].Position);
        Assert.Equal("Third segment", segments[2].Content);

        // 2. Reduce segment count (updates 0, 1; deletes 2)
        var updatedContent = "Updated first\n---\nUpdated second";
        draft.Content = updatedContent;
        await new DraftsService(_db, null!, null!, null!, null!).ReconcileSegmentsAsync(draft, updatedContent);
        await _db.SaveChangesAsync();

        segments = await _db.ThreadSegments
            .Where(s => s.DraftId == draft.Id)
            .OrderBy(s => s.Position)
            .ToListAsync();

        Assert.Equal(2, segments.Count);
        Assert.Equal("Updated first", segments[0].Content);
        Assert.Equal("Updated second", segments[1].Content);

        // 3. Expand segment count (updates 0, 1; inserts 2, 3)
        var expandedContent = "One\n---\nTwo\n---\nThree\n---\nFour";
        draft.Content = expandedContent;
        await new DraftsService(_db, null!, null!, null!, null!).ReconcileSegmentsAsync(draft, expandedContent);
        await _db.SaveChangesAsync();

        segments = await _db.ThreadSegments
            .Where(s => s.DraftId == draft.Id)
            .OrderBy(s => s.Position)
            .ToListAsync();

        Assert.Equal(4, segments.Count);
        Assert.Equal("One", segments[0].Content);
        Assert.Equal("Two", segments[1].Content);
        Assert.Equal("Three", segments[2].Content);
        Assert.Equal("Four", segments[3].Content);
    }

    [Fact]
    public async Task PlatformThread_CascadeDeletesAndUniqueConstraints()
    {
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = "Test Draft",
            UserId = _user.Id,
            Content = "First segment"
        };
        _db.Drafts.Add(draft);
        await _db.SaveChangesAsync();

        var thread1 = new PlatformThread
        {
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Draft,
            Content = "First segment"
        };
        _db.PlatformThreads.Add(thread1);
        await _db.SaveChangesAsync();

        // 1. Verify uniqueness constraint (cannot add duplicate platform thread for same draft)
        var thread2 = new PlatformThread
        {
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Sent
        };
        _db.PlatformThreads.Add(thread2);
        await Assert.ThrowsAsync<DbUpdateException>(async () => await _db.SaveChangesAsync());

        // Clear local tracking error to continue
        _db.Entry(thread2).State = EntityState.Detached;

        // 2. Cascade delete verification
        _db.Drafts.Remove(draft);
        await _db.SaveChangesAsync();

        var threadsCount = await _db.PlatformThreads.CountAsync(t => t.DraftId == draft.Id);
        Assert.Equal(0, threadsCount);
    }

    [Fact]
    public async Task ReconcileSourcesAsync_SyncsReferencesCorrectly()
    {
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = "Test Draft",
            UserId = _user.Id,
            Content = ""
        };
        _db.Drafts.Add(draft);
        await _db.SaveChangesAsync();

        var fileSource = new Source
        {
            Id = Guid.NewGuid(),
            Kind = SourceKind.File,
            Reference = "document.pdf",
            Title = "document.pdf",
            Content = "Extracted PDF content"
        };
        _db.Sources.Add(fileSource);
        _db.DraftSources.Add(new DraftSource { DraftId = draft.Id, SourceId = fileSource.Id });
        await _db.SaveChangesAsync();

        var content = $"Check out my doc: [Doc](file://{fileSource.Id})";
        draft.Content = content;

        await new SourcesService(_db, null!, null!, null!).ReconcileSourcesAsync(draft, content);

        var sources = await _db.Sources.Where(s => s.DraftSources.Any(ds => ds.DraftId == draft.Id)).ToListAsync();
        Assert.Single(sources);
        Assert.Equal(fileSource.Id, sources[0].Id);

        var content2 = "No file links anymore!";
        draft.Content = content2;

        await new SourcesService(_db, null!, null!, null!).ReconcileSourcesAsync(draft, content2);

        sources = await _db.Sources.Where(s => s.DraftSources.Any(ds => ds.DraftId == draft.Id)).ToListAsync();
        Assert.Empty(sources);
    }

    [Fact]
    public void AnalyzeSegmentMedia_IdentifiesMediaAndYouTube()
    {
        var mediaGuid = Guid.NewGuid();
        
        // 1. Text only
        var res1 = DraftsEndpoint.AnalyzeSegmentMedia("This is a simple post without media.");
        Assert.Empty(res1.ImageIds);
        Assert.Null(res1.YouTubeUrl);
        Assert.False(res1.HasConflict);

        // 2. Images only
        var res2 = DraftsEndpoint.AnalyzeSegmentMedia($"This has an image: ![image](media://{mediaGuid})");
        Assert.Single(res2.ImageIds);
        Assert.Equal(mediaGuid, res2.ImageIds[0]);
        Assert.Null(res2.YouTubeUrl);
        Assert.False(res2.HasConflict);

        // 3. YouTube only
        var res3 = DraftsEndpoint.AnalyzeSegmentMedia("Check this out: ![](https://www.youtube.com/watch?v=Ij4oKVn1Qso)");
        Assert.Empty(res3.ImageIds);
        Assert.Equal("https://www.youtube.com/watch?v=Ij4oKVn1Qso", res3.YouTubeUrl);
        Assert.False(res3.HasConflict);

        // 4. Both (Conflict)
        var res4 = DraftsEndpoint.AnalyzeSegmentMedia($"Check this out: ![](https://youtu.be/Ij4oKVn1Qso) and this image: ![img](media://{mediaGuid})");
        Assert.Single(res4.ImageIds);
        Assert.Equal(mediaGuid, res4.ImageIds[0]);
        Assert.Equal("https://youtu.be/Ij4oKVn1Qso", res4.YouTubeUrl);
        Assert.True(res4.HasConflict);
    }
}
