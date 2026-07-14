using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Infrastructure;
using SocialWorker.Api.Infrastructure.Background;
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

public sealed record DraftDto(
    Guid Id,
    string Title,
    string Status,
    string? Content,
    string? TargetPlatform,
    Guid? CanonicalDraftId,
    List<PlatformThreadDto> Threads,
    List<MediaAssetMiniDto> MediaAssets,
    string? ChatHistory,
    string? ChatSummary,
    int LastSummarizedMessageCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed class DraftsService
{
    private static readonly Regex YouTubeEmbedRegex = new(@"!\[.*?\]\((https?://(?:www\.)?youtube\.com/watch\?v=[\w-]+|https?://youtu\.be/[\w-]+)\)", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly FileStorageProvider _storage;
    private readonly SourcesService _sourcesService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BackgroundJobQueue _queue;

    public DraftsService(
        AppDbContext db,
        FileStorageProvider storage,
        SourcesService sourcesService,
        IServiceScopeFactory scopeFactory,
        BackgroundJobQueue queue)
    {
        _db = db;
        _storage = storage;
        _sourcesService = sourcesService;
        _scopeFactory = scopeFactory;
        _queue = queue;
    }

    private static DraftDto ToDto(Draft draft, List<PlatformThread> threads, List<Post> posts, List<MediaAsset> media)
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
            return ToDto(d, draftThreads, draftPosts, draftMedia);
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

        await ReconcileSegmentsAsync(draft, content ?? "", ct);
        await _db.SaveChangesAsync(ct);

        await _sourcesService.ReconcileSourcesAsync(draft, content ?? "");
        await _db.SaveChangesAsync(ct);

        return ToDto(draft, new List<PlatformThread> { thread }, new List<Post>(), new List<MediaAsset>());
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

        return ToDto(draft, threads, posts, media);
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
            await ReconcileSegmentsAsync(draft, content, ct);
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
            TriggerBackgroundSummarization(userId, id, chatHistory);
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

        return ToDto(draft, threads, posts, media);
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

    private void TriggerBackgroundSummarization(Guid userId, Guid draftId, string chatHistoryJson)
    {
        _queue.Enqueue(new BackgroundJobQueue.Job("chat-summary", async ct =>
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adapter = scope.ServiceProvider.GetRequiredService<ILlmProviderAdapter>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DraftsService>>();
            var providerService = scope.ServiceProvider.GetRequiredService<LlmProviderService>();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (user == null) return;

            var provider = await providerService.GetProviderForUserAsync(db, user);
            if (provider == null) return;

            var credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);

            using var doc = JsonDocument.Parse(chatHistoryJson);
            if (!doc.RootElement.TryGetProperty("messages", out var messagesArray) || messagesArray.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var totalCount = messagesArray.GetArrayLength();
            var totalTokens = messagesArray.EnumerateArray().Sum(ChatContextWindowPolicy.EstimateStoredMessageTokens);

            var draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId);
            if (draft == null) return;

            var contextWindow = provider.ContextWindowTokens.GetValueOrDefault() > 0
                ? provider.ContextWindowTokens!.Value
                : ChatContextWindowPolicy.ResolveContextWindowTokens(provider.Model);
            var targetTailTokenBudget = Math.Clamp(contextWindow / 4, 1200, 12 * 1024);
            var shouldCompact = totalCount >= 20 || totalTokens > targetTailTokenBudget * 2;
            if (!shouldCompact)
            {
                return;
            }

            logger.LogInformation("Triggering compaction for draft {DraftId}. Total messages: {TotalCount}, estimated tokens: {TotalTokens}, target tail token budget: {TailBudget}", draftId, totalCount, totalTokens, targetTailTokenBudget);

            var tailMessages = new List<JsonElement>();
            var tailTokens = 0;
            for (var i = totalCount - 1; i >= 0; i--)
            {
                var message = messagesArray[i];
                var messageTokens = ChatContextWindowPolicy.EstimateStoredMessageTokens(message);
                if (tailMessages.Count > 0 && tailTokens + messageTokens > targetTailTokenBudget)
                {
                    break;
                }

                tailMessages.Add(message);
                tailTokens += messageTokens;
            }

            tailMessages.Reverse();
            int messagesToSummarize = Math.Max(0, totalCount - tailMessages.Count);
            if (messagesToSummarize <= 0) return;

            var existingSummaryContext = "";
            if (!string.IsNullOrEmpty(draft.ChatSummary))
            {
                existingSummaryContext = $"Existing Summary of earlier conversation:\n{draft.ChatSummary}\n\n";
            }

            var sb = new StringBuilder();
            sb.AppendLine("Please read this conversation history and provide a concise, structured summary. Focus on key user instructions, preferences, decisions, and outcomes that the assistant should remember. Do not lose context. Keep the summary under 300 words.");
            sb.AppendLine(existingSummaryContext);
            sb.AppendLine("New Conversation History:");

            for (int i = 0; i < messagesToSummarize; i++)
            {
                var msg = messagesArray[i];
                var role = msg.TryGetProperty("role", out var r) ? r.GetString() : "unknown";

                var contentText = "";
                if (msg.TryGetProperty("content", out var contentProp))
                {
                    if (contentProp.ValueKind == JsonValueKind.String)
                    {
                        contentText = contentProp.GetString() ?? "";
                    }
                    else if (contentProp.ValueKind == JsonValueKind.Array)
                    {
                        var parts = new List<string>();
                        foreach (var part in contentProp.EnumerateArray())
                        {
                            if (part.TryGetProperty("text", out var t))
                            {
                                parts.Add(t.GetString() ?? "");
                            }
                        }
                        contentText = string.Join(" ", parts);
                    }
                }

                sb.AppendLine($"{role}: {contentText}");
            }

            var summarizeRequest = new OpenAiModels.ChatCompletionRequest
            {
                Model = credentials.Model,
                Messages = new List<OpenAiModels.OpenAiMessage>
                {
                    new() { Role = "system", Content = "You are a helpful assistant that summarizes chat conversations to keep context tight." },
                    new() { Role = "user", Content = sb.ToString() }
                },
                Stream = false
            };

            var response = await adapter.CompleteAsync(summarizeRequest, credentials, ct);
            if (response?.Choices != null && response.Choices.Count > 0)
            {
                var newSummary = response.Choices[0].Message.Content?.ToString();
                if (!string.IsNullOrEmpty(newSummary))
                {
                    var historyNode = JsonNode.Parse(chatHistoryJson)?.AsObject();
                    var messageNodes = new JsonArray();
                    foreach (var message in tailMessages)
                    {
                        var cloned = JsonNode.Parse(message.GetRawText());
                        if (cloned != null)
                        {
                            messageNodes.Add(cloned);
                        }
                    }

                    if (historyNode != null)
                    {
                        historyNode["messages"] = messageNodes;
                        draft.ChatHistory = historyNode.ToJsonString();
                    }

                    draft.ChatSummary = newSummary;
                    draft.LastSummarizedMessageCount = 0;
                    draft.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("Compaction completed for draft {DraftId}. Summary covers {SummarizedCount} messages, raw history trimmed to {TailCount} messages.", draftId, messagesToSummarize, tailMessages.Count);
                }
            }
        }));
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
        var imageIds = SharedPatterns.MediaRegex.Matches(segmentContent)
            .Select(m => Guid.TryParse(m.Groups[2].Value, out var guid) ? guid : Guid.Empty)
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
