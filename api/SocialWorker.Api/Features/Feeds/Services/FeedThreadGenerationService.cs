using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Chat.Services;
using SocialWorker.Api.Features.Drafts;

namespace SocialWorker.Api.Features.Feeds;

public class FeedThreadGenerationService
{
    private readonly AppDbContext _db;
    private readonly ChatService _chatService;
    private readonly ILogger<FeedThreadGenerationService> _logger;

    public FeedThreadGenerationService(
        AppDbContext db,
        ChatService chatService,
        ILogger<FeedThreadGenerationService> logger)
    {
        _db = db;
        _chatService = chatService;
        _logger = logger;
    }

    public Task<bool> GenerateThreadAsync(
        Draft draft,
        string itemLink,
        string instructionPrompt,
        Guid userId,
        CancellationToken ct)
    {
        return GenerateCanonicalThreadAsync(draft, itemLink, instructionPrompt, userId, ct);
    }

    public virtual async Task<bool> GenerateCanonicalThreadAsync(
        Draft draft,
        string itemLink,
        string instructionPrompt,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            draft.Status = DraftStatus.Formatting;
            await _db.SaveChangesAsync(ct);

            var promptText = $"Please draft a thread based on the available sources. Follow these instructions:\n{instructionPrompt}";
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
            };

            var assistantTextBuilder = new System.Text.StringBuilder();
            await foreach (var line in _chatService.StreamAsync(chatRequest, userId, ct))
            {
                if (line.StartsWith("0:"))
                {
                    try
                    {
                        var json = line.Substring(2).Trim();
                        var text = JsonSerializer.Deserialize<string>(json);
                        if (!string.IsNullOrEmpty(text))
                        {
                            assistantTextBuilder.Append(text);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to deserialize streaming text line");
                    }
                }
            }

            var finalDraft = await _db.Drafts
                .Include(d => d.Threads)
                .FirstOrDefaultAsync(d => d.Id == draft.Id, ct);

            if (finalDraft == null)
            {
                _logger.LogError("Draft {DraftId} was deleted during LLM execution.", draft.Id);
                return false;
            }

            var assistantText = assistantTextBuilder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(assistantText))
            {
                assistantText = "Drafted canonical thread segments based on feed source and instructions.";
            }

            var historyPayload = new ChatModels.ChatHistoryPayload
            {
                Messages = new List<ChatModels.StoredChatMessage>
                {
                    new ChatModels.StoredChatMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Role = "user",
                        Content = new List<ChatModels.StoredChatPart>
                        {
                            new ChatModels.StoredChatPart
                            {
                                Type = "text",
                                Text = promptText
                            }
                        }
                    },
                    new ChatModels.StoredChatMessage
                    {
                        Id = Guid.NewGuid().ToString(),
                        Role = "assistant",
                        Content = new List<ChatModels.StoredChatPart>
                        {
                            new ChatModels.StoredChatPart
                            {
                                Type = "text",
                                Text = assistantText
                            }
                        }
                    }
                }
            };

            finalDraft.ChatHistory = JsonSerializer.Serialize(historyPayload);
            finalDraft.Status = DraftStatus.Editing;
            await _db.SaveChangesAsync(ct);

            draft.Content = finalDraft.Content;
            draft.Status = finalDraft.Status;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in canonical thread generation for draft {DraftId}", draft.Id);
            draft.Status = DraftStatus.Failed;
            await _db.SaveChangesAsync(ct);
            return false;
        }
    }

    public virtual async Task<Draft?> GeneratePlatformVariantDraftAsync(
        Draft canonicalDraft,
        AutomationWorkflowDestination destination,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            // Ensure fresh canonical draft content is loaded
            var freshCanonical = await _db.Drafts
                .FirstOrDefaultAsync(d => d.Id == canonicalDraft.Id, ct) ?? canonicalDraft;

            Enum.TryParse<SocialPlatform>(destination.Platform, true, out var platform);

            var variantDraft = new Draft
            {
                Title = $"{freshCanonical.Title} ({destination.Platform})",
                Content = freshCanonical.Content,
                Status = DraftStatus.Formatting,
                UserId = userId,
                TargetPlatform = platform,
                CanonicalDraftId = freshCanonical.Id,
                AutomationWorkflowId = freshCanonical.AutomationWorkflowId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Drafts.Add(variantDraft);
            await _db.SaveChangesAsync(ct);

            var thread = new PlatformThread
            {
                DraftId = variantDraft.Id,
                Platform = destination.Platform,
                Stage = PlatformThreadStage.Draft,
                Content = freshCanonical.Content ?? string.Empty
            };
            _db.PlatformThreads.Add(thread);

            // Copy canonical segments to variant
            var canonicalSegments = await _db.ThreadSegments
                .Where(s => s.DraftId == freshCanonical.Id)
                .OrderBy(s => s.Position)
                .ToListAsync(ct);

            if (canonicalSegments.Count > 0)
            {
                foreach (var seg in canonicalSegments)
                {
                    _db.ThreadSegments.Add(new ThreadSegment
                    {
                        DraftId = variantDraft.Id,
                        Position = seg.Position,
                        Content = seg.Content
                    });
                }
            }
            else if (!string.IsNullOrWhiteSpace(freshCanonical.Content))
            {
                var segments = DraftSegmentService.SplitMarkdownIntoSegments(freshCanonical.Content);
                for (int i = 0; i < segments.Count; i++)
                {
                    _db.ThreadSegments.Add(new ThreadSegment
                    {
                        DraftId = variantDraft.Id,
                        Position = i,
                        Content = segments[i]
                    });
                }
            }

            variantDraft.Status = DraftStatus.Editing;
            await _db.SaveChangesAsync(ct);

            return variantDraft;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate platform variant draft for destination {Platform} on canonical draft {DraftId}",
                destination.Platform, canonicalDraft.Id);
            return null;
        }
    }
}
