using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Chat.Services;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Sources;

namespace SocialWorker.Api.Features.Feeds;

public sealed class FeedOrchestrationService
{
    private readonly AppDbContext _db;
    private readonly SourcesService _sourcesService;
    private readonly ChatService _chatService;
    private readonly IPublisherResolver _publisherResolver;
    private readonly ILogger<FeedOrchestrationService> _logger;

    public FeedOrchestrationService(
        AppDbContext db,
        SourcesService sourcesService,
        ChatService chatService,
        IPublisherResolver publisherResolver,
        ILogger<FeedOrchestrationService> logger)
    {
        _db = db;
        _sourcesService = sourcesService;
        _chatService = chatService;
        _publisherResolver = publisherResolver;
        _logger = logger;
    }

    public async Task ProcessFeedItemAsync(
        FeedSubscription subscription,
        string itemTitle,
        string itemLink,
        string itemDescription,
        DateTime? itemPublishDate,
        CancellationToken ct)
    {
        var userId = subscription.UserId;

        // 1. Duplicate check
        var isDuplicate = await _db.Sources.AnyAsync(s => 
            s.Reference == itemLink && 
            s.DraftSources.Any(ds => ds.Draft.UserId == userId && ds.Draft.Status != DraftStatus.Deleted), 
            ct);

        if (isDuplicate)
        {
            _logger.LogInformation("Skipping duplicate feed item link: {Link}", itemLink);
            return;
        }

        // 2. Filter check
        if (!PassesFilters(itemTitle, itemDescription, subscription.IncludeFilters, subscription.ExcludeFilters))
        {
            _logger.LogInformation("Skipping feed item {Link} because it does not match filters.", itemLink);
            return;
        }

        _logger.LogInformation("Processing new feed item: {Link}", itemLink);

        // 3. Create Draft in Sourcing stage
        var draft = new Draft
        {
            Title = string.IsNullOrWhiteSpace(itemTitle) ? "Untitled Feed Item" : itemTitle,
            Status = DraftStatus.Sourcing,
            UserId = userId,
            TargetPlatform = SocialPlatform.Bluesky
        };
        _db.Drafts.Add(draft);
        await _db.SaveChangesAsync(ct);

        // 4. Create initial PlatformThread
        var thread = new PlatformThread
        {
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Draft,
            Content = string.Empty
        };
        _db.PlatformThreads.Add(thread);
        await _db.SaveChangesAsync(ct);

        // 5. Trigger automated ingestion / scraping
        Guid sourceId;
        try
        {
            var addResult = await _sourcesService.AddUrlSourceAsync(userId, draft.Id, itemLink, itemTitle, null, ct);
            sourceId = addResult.SourceId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest source URL {Link} for draft {DraftId}. Aborting orchestration.", itemLink, draft.Id);
            draft.Status = DraftStatus.Failed;
            draft.Title = $"[Failed Ingestion] {draft.Title}";
            await _db.SaveChangesAsync(ct);
            return;
        }

        // 6. Ingestion Gate: Wait for source processing (specifically YouTube transcription)
        var isYouTube = false;
        var source = await _db.Sources.FindAsync(new object[] { sourceId }, ct);
        if (source != null && source.Kind == SourceKind.YouTube)
        {
            isYouTube = true;
        }

        if (isYouTube)
        {
            var startTime = DateTime.UtcNow;
            var timeout = TimeSpan.FromMinutes(10);
            while (true)
            {
                if (DateTime.UtcNow - startTime > timeout)
                {
                    _logger.LogError("Ingestion gate timeout waiting for YouTube transcription of source {SourceId}", sourceId);
                    draft.Status = DraftStatus.Failed;
                    draft.Title = $"[Timeout Ingestion] {draft.Title}";
                    await _db.SaveChangesAsync(ct);
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                
                // Reload source from DB
                var currentSource = await _db.Sources.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sourceId, ct);
                if (currentSource == null)
                {
                    _logger.LogError("Source {SourceId} was deleted during ingestion waiting.", sourceId);
                    draft.Status = DraftStatus.Failed;
                    await _db.SaveChangesAsync(ct);
                    return;
                }

                if (currentSource.ProcessingStatus == SourceProcessingStatus.Complete)
                {
                    break;
                }

                if (currentSource.ProcessingStatus == SourceProcessingStatus.Failed)
                {
                    _logger.LogError("YouTube transcription failed for source {SourceId}.", sourceId);
                    draft.Status = DraftStatus.Failed;
                    draft.Title = $"[Failed Ingestion] {draft.Title}";
                    await _db.SaveChangesAsync(ct);
                    return;
                }
            }
        }

        // 7. Headless LLM execution loop
        try
        {
            // Transition status to Formatting so editor panel lock is properly respected or simulation works
            draft.Status = DraftStatus.Formatting;
            await _db.SaveChangesAsync(ct);

            var promptText = $"Please draft a thread based on the source link: {itemLink}. Follow these instructions:\n{subscription.InstructionPrompt}";
            var chatRequest = new ChatModels.ChatRequest
            {
                DraftId = draft.Id,
                Messages = new List<ChatModels.UiMessage>
                {
                    new ChatModels.UiMessage
                    {
                        Role = "user",
                        Content = new List<ChatModels.UiPart>
                        {
                            new ChatModels.UiPart
                            {
                                Type = "text",
                                Text = promptText
                            }
                        }
                    }
                },
                UnstableAssistantMessageId = Guid.NewGuid().ToString()
            }
;
            // Consume the stream fully to execute tool calls
            var assistantTextBuilder = new System.Text.StringBuilder();
            await foreach (var line in _chatService.StreamAsync(chatRequest, userId, ct))
            {
                if (line.StartsWith("0:"))
                {
                    try
                    {
                        var json = line.Substring(2).Trim();
                        var text = System.Text.Json.JsonSerializer.Deserialize<string>(json);
                        if (!string.IsNullOrEmpty(text))
                        {
                            assistantTextBuilder.Append(text);
                        }
                    }
                    catch
                    {
                        // Best effort
                    }
                }
            }

            // Reload draft to get updated content
            var finalDraft = await _db.Drafts
                .Include(d => d.Threads)
                .FirstOrDefaultAsync(d => d.Id == draft.Id, ct);

            if (finalDraft == null)
            {
                _logger.LogError("Draft {DraftId} was deleted during LLM execution.", draft.Id);
                return;
            }

            // Save the chat history so the user can review it in the UI chat
            var assistantText = assistantTextBuilder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(assistantText))
            {
                assistantText = "Drafted thread segments based on feed source and instructions.";
            }

            var historyObj = new
            {
                messages = new[]
                {
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        role = "user",
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = promptText
                            }
                        }
                    },
                    new
                    {
                        id = Guid.NewGuid().ToString(),
                        role = "assistant",
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = assistantText
                            }
                        }
                    }
                }
            };

            finalDraft.ChatHistory = System.Text.Json.JsonSerializer.Serialize(historyObj);

            // Mark draft as Editing (ready for manual review/editing)
            finalDraft.Status = DraftStatus.Editing;
            await _db.SaveChangesAsync(ct);

            // 8. AutoPublish (if requested)
            if (subscription.AutoPublish)
            {
                var blueskyThread = finalDraft.Threads.FirstOrDefault(t => t.Platform == "Bluesky");
                if (blueskyThread != null && !string.IsNullOrWhiteSpace(blueskyThread.Content))
                {
                    var account = await _db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId && a.Platform == "Bluesky", ct);
                    if (account == null)
                    {
                        _logger.LogError("AutoPublish enabled for feed subscription but no Bluesky account was found for user {UserId}", userId);
                        return;
                    }

                    var publisher = _publisherResolver.Resolve("Bluesky");
                    if (publisher != null)
                    {
                        _logger.LogInformation("Auto-publishing draft {DraftId} to Bluesky...", finalDraft.Id);
                        var publishResult = await publisher.PublishAsync(blueskyThread, account, ct);
                        if (publishResult.Success)
                        {
                            foreach (var publishedPost in publishResult.Posts)
                            {
                                var post = new Post
                                {
                                    DraftId = finalDraft.Id,
                                    PlatformThreadId = blueskyThread.Id,
                                    SegmentIndex = publishedPost.SegmentIndex,
                                    Platform = "Bluesky",
                                    RemoteId = publishedPost.RemoteId,
                                    Url = publishedPost.Url
                                };
                                _db.Posts.Add(post);
                            }
                            blueskyThread.Stage = PlatformThreadStage.Sent;
                            blueskyThread.UpdatedAt = DateTime.UtcNow;
                            await _db.SaveChangesAsync(ct);
                            _logger.LogInformation("Successfully auto-published draft {DraftId}.", finalDraft.Id);
                        }
                        else
                        {
                            _logger.LogError("AutoPublish failed for draft {DraftId}: {Error}", finalDraft.Id, publishResult.ErrorMessage);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in headless LLM orchestration for draft {DraftId}", draft.Id);
            draft.Status = DraftStatus.Failed;
            await _db.SaveChangesAsync(ct);
        }
    }

    private static bool PassesFilters(string title, string description, string? includeFilters, string? excludeFilters)
    {
        var textToMatch = $"{(title ?? "")} {(description ?? "")}";

        // Process Excludes first
        if (!string.IsNullOrWhiteSpace(excludeFilters))
        {
            var excludes = excludeFilters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var exc in excludes)
            {
                if (textToMatch.Contains(exc, StringComparison.OrdinalIgnoreCase))
                {
                    return false; // Skip if any exclude matches
                }
            }
        }

        // Process Includes second
        if (!string.IsNullOrWhiteSpace(includeFilters))
        {
            var includes = includeFilters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var inc in includes)
            {
                if (textToMatch.Contains(inc, StringComparison.OrdinalIgnoreCase))
                {
                    return true; // Match found, passes filters
                }
            }
            return false; // None of the include filters matched
        }

        return true; // No include filters means everything is allowed
    }
}
