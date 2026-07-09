using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Sources;

namespace SocialWorker.Api.Features.Drafts;

public sealed record PostDto(Guid Id, Guid PlatformThreadId, int SegmentIndex, string Platform, string RemoteId, string Url);

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

public sealed record DraftDto(
    Guid Id,
    string Title,
    string Status,
    string? Content,
    List<PlatformThreadDto> Threads,
    List<MediaAssetMiniDto> MediaAssets,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed class DraftsService
{
    private static readonly Regex MediaRegex = new(@"!\[.*?\]\(media://([0-9a-fA-F\-]{36})\)", RegexOptions.Compiled);
    private static readonly Regex YouTubeEmbedRegex = new(@"!\[.*?\]\((https?://(?:www\.)?youtube\.com/watch\?v=[\w-]+|https?://youtu\.be/[\w-]+)\)", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly FileStorageProvider _storage;
    private readonly SourcesService _sourcesService;
    private readonly IServiceScopeFactory _scopeFactory;

    public DraftsService(
        AppDbContext db,
        FileStorageProvider storage,
        SourcesService sourcesService,
        IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _storage = storage;
        _sourcesService = sourcesService;
        _scopeFactory = scopeFactory;
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

        var threadIds = threads.Select(t => t.Id).ToList();
        var posts = await _db.Posts
            .Where(p => threadIds.Contains(p.PlatformThreadId))
            .ToListAsync(ct);

        return drafts.Select(d => new DraftDto(
            d.Id,
            d.Title,
            d.Status.ToString(),
            d.Content,
            threads.Where(t => t.DraftId == d.Id)
                .Select(t => new PlatformThreadDto(
                    t.Id, t.DraftId, t.Platform, t.Stage.ToString(), t.Content,
                    posts.Where(p => p.PlatformThreadId == t.Id)
                         .Select(p => new PostDto(p.Id, p.PlatformThreadId, p.SegmentIndex, p.Platform, p.RemoteId, p.Url))
                         .ToList()))
                .ToList(),
            media.Where(m => m.DraftId == d.Id)
                .Select(m => new MediaAssetMiniDto(m.Id, m.DraftId, m.FileName, m.MimeType, m.AltText, m.FilePath, m.SizeBytes, m.Width, m.Height, m.CreatedAt))
                .ToList(),
            d.CreatedAt,
            d.UpdatedAt
        )).ToList();
    }

    public async Task<DraftDto> CreateDraftAsync(Guid userId, string? title, string? content, CancellationToken ct)
    {
        var draft = new Draft
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Untitled" : title,
            Content = content,
            UserId = userId
        };
        _db.Drafts.Add(draft);
        await _db.SaveChangesAsync(ct);

        var thread = new PlatformThread
        {
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Draft,
            Content = content
        };
        _db.PlatformThreads.Add(thread);
        await _db.SaveChangesAsync(ct);

        await ReconcileSegmentsAsync(draft, content ?? "", ct);
        await _db.SaveChangesAsync(ct);

        await _sourcesService.ReconcileSourcesAsync(draft, content ?? "");
        await _db.SaveChangesAsync(ct);

        return new DraftDto(
            draft.Id,
            draft.Title,
            draft.Status.ToString(),
            draft.Content,
            new List<PlatformThreadDto> { new(thread.Id, thread.DraftId, thread.Platform, thread.Stage.ToString(), thread.Content, new List<PostDto>()) },
            new List<MediaAssetMiniDto>(),
            draft.CreatedAt,
            draft.UpdatedAt
        );
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

        return new DraftDto(
            draft.Id,
            draft.Title,
            draft.Status.ToString(),
            draft.Content,
            threads.Select(t => new PlatformThreadDto(t.Id, t.DraftId, t.Platform, t.Stage.ToString(), t.Content,
                posts.Where(p => p.PlatformThreadId == t.Id)
                     .Select(p => new PostDto(p.Id, p.PlatformThreadId, p.SegmentIndex, p.Platform, p.RemoteId, p.Url))
                     .ToList())).ToList(),
            media.Select(m => new MediaAssetMiniDto(m.Id, m.DraftId, m.FileName, m.MimeType, m.AltText, m.FilePath, m.SizeBytes, m.Width, m.Height, m.CreatedAt)).ToList(),
            draft.CreatedAt,
            draft.UpdatedAt
        );
    }

    public async Task<DraftDto> UpdateDraftAsync(
        Guid userId,
        Guid id,
        string? title,
        string? content,
        string? statusStr,
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
            await ReconcileSegmentsAsync(draft, content, ct);
            await _sourcesService.ReconcileSourcesAsync(draft, content);
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

        draft.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var threads = await _db.PlatformThreads.Where(t => t.DraftId == id).ToListAsync(ct);
        var threadIds = threads.Select(t => t.Id).ToList();
        var posts = await _db.Posts.Where(p => threadIds.Contains(p.PlatformThreadId)).ToListAsync(ct);
        var media = await _db.MediaAssets.Where(m => m.DraftId == id).ToListAsync(ct);

        return new DraftDto(
            draft.Id,
            draft.Title,
            draft.Status.ToString(),
            draft.Content,
            threads.Select(t => new PlatformThreadDto(t.Id, t.DraftId, t.Platform, t.Stage.ToString(), t.Content,
                posts.Where(p => p.PlatformThreadId == t.Id)
                     .Select(p => new PostDto(p.Id, p.PlatformThreadId, p.SegmentIndex, p.Platform, p.RemoteId, p.Url))
                     .ToList())).ToList(),
            media.Select(m => new MediaAssetMiniDto(m.Id, m.DraftId, m.FileName, m.MimeType, m.AltText, m.FilePath, m.SizeBytes, m.Width, m.Height, m.CreatedAt)).ToList(),
            draft.CreatedAt,
            draft.UpdatedAt
        );
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
            if (stage == PlatformThreadStage.Sent)
            {
                if (string.Equals(thread.Platform, "Bluesky", StringComparison.OrdinalIgnoreCase))
                {
                    var segments = SplitMarkdownIntoSegments(thread.Content ?? "");
                    foreach (var segment in segments)
                    {
                        var analysis = AnalyzeSegmentMedia(segment);
                        if (analysis.HasConflict)
                        {
                            throw new InvalidOperationException("Bluesky segment contains both images and a YouTube embed. Only images OR one YouTube embed is allowed per post.");
                        }
                    }
                }
            }
            thread.Stage = stage;
        }

        thread.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        var posts = await _db.Posts.Where(p => p.PlatformThreadId == threadId).ToListAsync(ct);
        return new PlatformThreadDto(thread.Id, thread.DraftId, thread.Platform, thread.Stage.ToString(), thread.Content,
            posts.Select(p => new PostDto(p.Id, p.PlatformThreadId, p.SegmentIndex, p.Platform, p.RemoteId, p.Url)).ToList());
    }

    public async Task ReconcileSegmentsAsync(Draft draft, string markdown, CancellationToken ct = default)
    {
        var rawSegments = SplitMarkdownIntoSegments(markdown);
        var existing = await _db.ThreadSegments
            .Where(s => s.DraftId == draft.Id)
            .OrderBy(s => s.Position)
            .ToListAsync(ct);

        int max = Math.Max(rawSegments.Count, existing.Count);
        for (int i = 0; i < max; i++)
        {
            if (i < rawSegments.Count)
            {
                var content = rawSegments[i];
                if (i < existing.Count)
                {
                    existing[i].Content = content;
                }
                else
                {
                    _db.ThreadSegments.Add(new ThreadSegment
                    {
                        DraftId = draft.Id,
                        Position = i,
                        Content = content
                    });
                }
            }
            else
            {
                _db.ThreadSegments.Remove(existing[i]);
            }
        }
    }

    public static List<string> SplitMarkdownIntoSegments(string markdown)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(markdown))
        {
            return new List<string> { "" };
        }

        var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var current = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            if (line.Trim() == "---")
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                if (current.Length > 0)
                {
                    current.AppendLine();
                }
                current.Append(line);
            }
        }
        result.Add(current.ToString().Trim());
        return result;
    }

    public static SegmentMediaAnalysis AnalyzeSegmentMedia(string segmentContent)
    {
        var imageIds = MediaRegex.Matches(segmentContent)
            .Select(m => Guid.TryParse(m.Groups[1].Value, out var guid) ? guid : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToArray();

        string? youtubeUrl = null;
        var ytMatch = YouTubeEmbedRegex.Match(segmentContent);
        if (ytMatch.Success)
        {
            youtubeUrl = ytMatch.Groups[1].Value;
        }

        bool hasConflict = imageIds.Length > 0 && youtubeUrl != null;

        return new SegmentMediaAnalysis(imageIds, youtubeUrl, hasConflict);
    }

    public record SegmentMediaAnalysis(Guid[] ImageIds, string? YouTubeUrl, bool HasConflict);
}
