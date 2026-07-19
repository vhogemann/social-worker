using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Infrastructure;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Features.Drafts;

public sealed record PostDto(Guid Id, Guid PlatformThreadId, int SegmentIndex, string Platform, string? RemoteId, string? Url);

public sealed record PlatformThreadDto(Guid Id, Guid DraftId, string Platform, string Stage, string? Content, List<PostDto> Posts);

public sealed record MediaAssetMiniDto(
    Guid Id,
    Guid DraftId,
    string FileName,
    string MimeType,
    string? AltText,
    string FilePath,
    long SizeBytes,
    int Width,
    int Height,
    DateTime CreatedAt
);

public sealed record BlueskyReplyTargetDto(
    string ReplyRootUri,
    string ReplyRootCid,
    string ReplyParentUri,
    string ReplyParentCid,
    string? ReplyParentUrl,
    string? ReplyParentAuthor,
    string? ReplyParentText,
    string? ReplyParentAvatarUrl
);

public sealed record DraftDto(
    Guid Id,
    string Title,
    string Status,
    string? Content,
    string? TargetPlatform,
    Guid? CanonicalDraftId,
    List<PlatformThreadDto> Threads,
    List<MediaAssetMiniDto> MediaAssets,
    BlueskyReplyTargetDto? BlueskyReplyTarget,
    string? ChatHistory,
    string? ChatSummary,
    int LastSummarizedMessageCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed class DraftsService
{
    private readonly AppDbContext _db;
    private readonly FileStorageProvider _storage;
    private readonly SourcesService _sourcesService;
    private readonly DraftSegmentService _draftSegmentService;
    private readonly DraftChatSummaryService _draftChatSummaryService;

    public DraftsService(
        AppDbContext db,
        FileStorageProvider storage,
        SourcesService sourcesService,
        DraftSegmentService draftSegmentService,
        DraftChatSummaryService draftChatSummaryService)
    {
        _db = db;
        _storage = storage;
        _sourcesService = sourcesService;
        _draftSegmentService = draftSegmentService;
        _draftChatSummaryService = draftChatSummaryService;
    }

    private static DraftDto ToDto(Draft draft, List<PlatformThread> threads, List<Post> posts, List<MediaAsset> media, DraftBlueskyMetadata? blueskyMetadata = null)
    {
        return new DraftDto(
            draft.Id,
            draft.Title,
            draft.Status.ToString(),
            draft.Content,
            draft.TargetPlatform?.ToString(),
            draft.CanonicalDraftId,
            threads.Select(t => new PlatformThreadDto(
                t.Id, t.DraftId, t.Platform, t.Stage.ToString(), t.Content,
                posts.Where(p => p.PlatformThreadId == t.Id)
                     .Select(p => new PostDto(p.Id, p.PlatformThreadId, p.SegmentIndex, p.Platform, p.RemoteId, p.Url))
                     .ToList()))
                .ToList(),
            media.Select(m => new MediaAssetMiniDto(m.Id, m.DraftId, m.FileName, m.MimeType, m.AltText, m.FilePath, m.SizeBytes, m.Width, m.Height, m.CreatedAt))
                .ToList(),
            blueskyMetadata is null
                ? null
                : new BlueskyReplyTargetDto(
                    blueskyMetadata.ReplyRootUri ?? string.Empty,
                    blueskyMetadata.ReplyRootCid ?? string.Empty,
                    blueskyMetadata.ReplyParentUri ?? string.Empty,
                    blueskyMetadata.ReplyParentCid ?? string.Empty,
                    blueskyMetadata.ReplyParentUrl,
                    blueskyMetadata.ReplyParentAuthor,
                    blueskyMetadata.ReplyParentText,
                    blueskyMetadata.ReplyParentAvatarUrl),
            draft.ChatHistory,
            draft.ChatSummary,
            draft.LastSummarizedMessageCount,
            draft.CreatedAt,
            draft.UpdatedAt
        );
    }

    public async Task<List<DraftDto>> GetDraftsForUserAsync(Guid userId, CancellationToken ct)
    {
        var drafts = await _db.Drafts
            .Where(d => d.UserId == userId && d.Status != DraftStatus.Deleted)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(ct);

        var draftIds = drafts.Select(d => d.Id).ToList();

        var threads = await _db.PlatformThreads
            .Where(t => draftIds.Contains(t.DraftId))
            .ToListAsync(ct);

        var media = await _db.MediaAssets
            .Where(m => draftIds.Contains(m.DraftId))
            .ToListAsync(ct);

        var blueskyMetadata = await _db.DraftBlueskyMetadata
            .Where(m => draftIds.Contains(m.DraftId))
            .ToListAsync(ct);

        var threadIds = threads.Select(t => t.Id).ToList();
        var posts = await _db.Posts
            .Where(p => threadIds.Contains(p.PlatformThreadId))
            .ToListAsync(ct);

        return drafts.Select(d =>
        {
            var draftThreads = threads.Where(t => t.DraftId == d.Id).ToList();
            var threadIds = draftThreads.Select(t => t.Id).ToList();
            var draftPosts = posts.Where(p => threadIds.Contains(p.PlatformThreadId)).ToList();
            var draftMedia = media.Where(m => m.DraftId == d.Id).ToList();
            var draftBlueskyMetadata = blueskyMetadata.FirstOrDefault(m => m.DraftId == d.Id);
            return ToDto(d, draftThreads, draftPosts, draftMedia, draftBlueskyMetadata);
        }).ToList();
    }

    public async Task<DraftDto> CreateDraftAsync(Guid userId, string? title, string? content, string? targetPlatform, CancellationToken ct)
    {
        SocialPlatform? platform = null;
        if (!string.IsNullOrWhiteSpace(targetPlatform) && Enum.TryParse<SocialPlatform>(targetPlatform, true, out var parsed))
        {
            platform = parsed;
        }

        var draft = new Draft
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Untitled" : title,
            Content = content,
            UserId = userId,
            TargetPlatform = platform ?? SocialPlatform.Bluesky
        };
        _db.Drafts.Add(draft);
        await _db.SaveChangesAsync(ct);

        var platformName = draft.TargetPlatform?.ToString() ?? "Bluesky";
        var thread = new PlatformThread
        {
            DraftId = draft.Id,
            Platform = platformName,
            Stage = PlatformThreadStage.Draft,
            Content = content
        };
        _db.PlatformThreads.Add(thread);
        await _db.SaveChangesAsync(ct);

        await _draftSegmentService.ReconcileSegmentsAsync(draft, content ?? "", ct);
        await _db.SaveChangesAsync(ct);

        await _sourcesService.ReconcileSourcesAsync(draft, content ?? "");
        await _db.SaveChangesAsync(ct);

        return ToDto(draft, new List<PlatformThread> { thread }, new List<Post>(), new List<MediaAsset>(), null);
    }

    public async Task<DraftDto> GetDraftByIdAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var draft = await _db.Drafts
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
            ?? throw new KeyNotFoundException("Draft not found or access denied.");

        var threads = await _db.PlatformThreads.Where(t => t.DraftId == id).ToListAsync(ct);
        var threadIds = threads.Select(t => t.Id).ToList();
        var posts = await _db.Posts.Where(p => threadIds.Contains(p.PlatformThreadId)).ToListAsync(ct);
        var media = await _db.MediaAssets.Where(m => m.DraftId == id).ToListAsync(ct);
        var blueskyMetadata = await _db.DraftBlueskyMetadata.FirstOrDefaultAsync(m => m.DraftId == id, ct);

        return ToDto(draft, threads, posts, media, blueskyMetadata);
    }

    public async Task<DraftDto> UpdateDraftAsync(
        Guid userId,
        Guid id,
        string? title,
        string? content,
        string? statusStr,
        string? chatHistory,
        string? chatSummary,
        int? lastSummarizedMessageCount,
        CancellationToken ct)
    {
        var draft = await _db.Drafts
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
            ?? throw new KeyNotFoundException("Draft not found or access denied.");

        if (title is not null)
        {
            draft.Title = string.IsNullOrWhiteSpace(title) ? "Untitled" : title;
        }

        if (content is not null)
        {
            draft.Content = content;
            await _draftSegmentService.ReconcileSegmentsAsync(draft, content, ct);
            await _sourcesService.ReconcileSourcesAsync(draft, content);
            
            // Sync content to all platform threads for this draft
            var platformThreads = await _db.PlatformThreads.Where(t => t.DraftId == id).ToListAsync(ct);
            foreach (var thread in platformThreads)
            {
                thread.Content = content;
            }
        }

        if (statusStr is not null && Enum.TryParse<DraftStatus>(statusStr, true, out var status))
        {
            if (status == DraftStatus.Deleted)
            {
                var draftAssets = await _db.MediaAssets.Where(m => m.DraftId == draft.Id).ToListAsync(ct);
                foreach (var asset in draftAssets)
                {
                    var isShared = await _db.MediaAssets.AnyAsync(m => m.Id != asset.Id && m.FilePath == asset.FilePath, ct);
                    if (!isShared)
                    {
                        _storage.DeleteFileAndEmptyFolder(asset.FilePath);
                    }
                }

                _db.Drafts.Remove(draft);
            }
            else
            {
                draft.Status = status;
            }
        }

        if (chatHistory is not null)
        {
            draft.ChatHistory = chatHistory;
            _draftChatSummaryService.TriggerBackgroundSummarization(userId, id, chatHistory);
        }

        if (chatSummary is not null)
        {
            draft.ChatSummary = chatSummary;
        }

        if (lastSummarizedMessageCount is not null)
        {
            draft.LastSummarizedMessageCount = lastSummarizedMessageCount.Value;
        }

        draft.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var threads = await _db.PlatformThreads.Where(t => t.DraftId == id).ToListAsync(ct);
        var threadIds = threads.Select(t => t.Id).ToList();
        var posts = await _db.Posts.Where(p => threadIds.Contains(p.PlatformThreadId)).ToListAsync(ct);
        var media = await _db.MediaAssets.Where(m => m.DraftId == id).ToListAsync(ct);
        var blueskyMetadata = await _db.DraftBlueskyMetadata.FirstOrDefaultAsync(m => m.DraftId == id, ct);

        return ToDto(draft, threads, posts, media, blueskyMetadata);
    }

    public async Task<DraftDto> SetDraftBlueskyReplyTargetAsync(
        Guid userId,
        Guid draftId,
        string? replyRootUri,
        string? replyRootCid,
        string? replyParentUri,
        string? replyParentCid,
        string? replyParentUrl,
        string? replyParentAuthor,
        string? replyParentText,
        string? replyParentAvatarUrl,
        CancellationToken ct)
    {
        var draft = await _db.Drafts
            .FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
            ?? throw new KeyNotFoundException("Draft not found or access denied.");

        var hasSentThread = await _db.PlatformThreads
            .AnyAsync(t => t.DraftId == draftId && t.Stage == PlatformThreadStage.Sent, ct);
        if (hasSentThread)
        {
            throw new InvalidOperationException("Cannot set a reply target on a sent draft. Create a new reply draft instead.");
        }

        var rootUri = string.IsNullOrWhiteSpace(replyRootUri) ? null : replyRootUri.Trim();
        var rootCid = string.IsNullOrWhiteSpace(replyRootCid) ? null : replyRootCid.Trim();
        var parentUri = string.IsNullOrWhiteSpace(replyParentUri) ? null : replyParentUri.Trim();
        var parentCid = string.IsNullOrWhiteSpace(replyParentCid) ? null : replyParentCid.Trim();
        var parentUrl = string.IsNullOrWhiteSpace(replyParentUrl) ? null : replyParentUrl.Trim();
        var parentAuthor = string.IsNullOrWhiteSpace(replyParentAuthor) ? null : replyParentAuthor.Trim();
        var parentText = string.IsNullOrWhiteSpace(replyParentText) ? null : replyParentText.Trim();
        var parentAvatarUrl = string.IsNullOrWhiteSpace(replyParentAvatarUrl) ? null : replyParentAvatarUrl.Trim();

        var clear = rootUri is null
            && rootCid is null
            && parentUri is null
            && parentCid is null
            && parentUrl is null
            && parentAuthor is null
            && parentText is null
            && parentAvatarUrl is null;
        var metadata = await _db.DraftBlueskyMetadata.FirstOrDefaultAsync(m => m.DraftId == draftId, ct);

        if (metadata is not null)
        {
            throw new InvalidOperationException("Bluesky reply target is already set for this draft and cannot be changed.");
        }

        if (clear)
        {
            throw new ArgumentException("Reply target values are required.");
        }
        else
        {
            if (rootUri is null || rootCid is null || parentUri is null || parentCid is null)
            {
                throw new ArgumentException("ReplyRootUri, ReplyRootCid, ReplyParentUri, and ReplyParentCid are required to set a Bluesky reply target.");
            }

            if (metadata is null)
            {
                metadata = new DraftBlueskyMetadata { DraftId = draftId };
                _db.DraftBlueskyMetadata.Add(metadata);
            }

            metadata.ReplyRootUri = rootUri;
            metadata.ReplyRootCid = rootCid;
            metadata.ReplyParentUri = parentUri;
            metadata.ReplyParentCid = parentCid;
            metadata.ReplyParentUrl = parentUrl;
            metadata.ReplyParentAuthor = parentAuthor;
            metadata.ReplyParentText = parentText;
            metadata.ReplyParentAvatarUrl = parentAvatarUrl;
        }

        draft.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetDraftByIdAsync(userId, draftId, ct);
    }

    public async Task<DraftDto> CreateReplyDraftFromBlueskyPostUrlAsync(
        Guid userId,
        string url,
        string? title,
        string? content,
        IBlueskyReplyTargetResolver resolver,
        CancellationToken ct)
    {
        var created = await CreateDraftAsync(userId, title, content, "Bluesky", ct);

        return await SetDraftBlueskyReplyTargetFromUrlAsync(
            userId,
            created.Id,
            url,
            resolver,
            ct);
    }

    public async Task<DraftDto> SetDraftBlueskyReplyTargetFromUrlAsync(
        Guid userId,
        Guid draftId,
        string url,
        IBlueskyReplyTargetResolver resolver,
        CancellationToken ct)
    {
        var resolution = await resolver.ResolveAsync(url, ct);
        if (!resolution.Success)
        {
            throw new ArgumentException(resolution.Error ?? "Could not resolve Bluesky post URL.");
        }

        return await SetDraftBlueskyReplyTargetAsync(
            userId,
            draftId,
            resolution.ReplyRootUri,
            resolution.ReplyRootCid,
            resolution.ReplyParentUri,
            resolution.ReplyParentCid,
            resolution.ReplyParentUrl,
            resolution.ReplyParentAuthor,
            resolution.ReplyParentText,
            resolution.ReplyParentAvatarUrl,
            ct);
    }

    public async Task<List<PlatformThreadDto>> GetPlatformThreadsForDraftAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var draftExists = await _db.Drafts.AnyAsync(d => d.Id == id && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
        if (!draftExists)
        {
            throw new KeyNotFoundException("Draft not found or access denied.");
        }

        var threads = await _db.PlatformThreads
            .Where(t => t.DraftId == id)
            .ToListAsync(ct);

        var threadIds = threads.Select(t => t.Id).ToList();
        var posts = await _db.Posts.Where(p => threadIds.Contains(p.PlatformThreadId)).ToListAsync(ct);

        return threads.Select(t => new PlatformThreadDto(t.Id, t.DraftId, t.Platform, t.Stage.ToString(), t.Content,
            posts.Where(p => p.PlatformThreadId == t.Id)
                 .Select(p => new PostDto(p.Id, p.PlatformThreadId, p.SegmentIndex, p.Platform, p.RemoteId, p.Url))
                 .ToList())).ToList();
    }

    public async Task<PlatformThreadDto> CreatePlatformThreadAsync(Guid userId, Guid id, string platform, CancellationToken ct)
    {
        var draft = await _db.Drafts.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
            ?? throw new KeyNotFoundException("Draft not found or access denied.");

        if (string.IsNullOrWhiteSpace(platform))
        {
            throw new ArgumentException("Platform is required.");
        }

        var exists = await _db.PlatformThreads.AnyAsync(t => t.DraftId == id && t.Platform == platform, ct);
        if (exists)
        {
            throw new InvalidOperationException($"A thread variant for platform '{platform}' already exists.");
        }

        var thread = new PlatformThread
        {
            DraftId = id,
            Platform = platform,
            Stage = PlatformThreadStage.Draft,
            Content = draft.Content
        };

        _db.PlatformThreads.Add(thread);
        await _db.SaveChangesAsync(ct);

        return new PlatformThreadDto(thread.Id, thread.DraftId, thread.Platform, thread.Stage.ToString(), thread.Content, new List<PostDto>());
    }

    public async Task<PlatformThreadDto> UpdatePlatformThreadAsync(
        Guid userId,
        Guid id,
        Guid threadId,
        string? content,
        string? stageStr,
        CancellationToken ct)
    {
        var draftExists = await _db.Drafts.AnyAsync(d => d.Id == id && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
        if (!draftExists)
        {
            throw new KeyNotFoundException("Draft not found or access denied.");
        }

        var thread = await _db.PlatformThreads.FirstOrDefaultAsync(t => t.Id == threadId && t.DraftId == id, ct)
            ?? throw new KeyNotFoundException("Thread not found.");

        if (content is not null)
        {
            thread.Content = content;
        }

        if (stageStr is not null && Enum.TryParse<PlatformThreadStage>(stageStr, true, out var stage))
        {
            thread.Stage = stage;
        }

        thread.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var posts = await _db.Posts.Where(p => p.PlatformThreadId == threadId).ToListAsync(ct);
        return new PlatformThreadDto(thread.Id, thread.DraftId, thread.Platform, thread.Stage.ToString(), thread.Content,
            posts.Select(p => new PostDto(p.Id, p.PlatformThreadId, p.SegmentIndex, p.Platform, p.RemoteId, p.Url)).ToList());
    }

    public Task ReconcileSegmentsAsync(Draft draft, string markdown, CancellationToken ct = default)
    {
        return _draftSegmentService.ReconcileSegmentsAsync(draft, markdown, ct);
    }

}
