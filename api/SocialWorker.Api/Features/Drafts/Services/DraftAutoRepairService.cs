using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Adapters;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure.Llm;
using SocialWorker.Api.Infrastructure.Platforms;

namespace SocialWorker.Api.Features.Drafts.Services;

public class DraftAutoRepairService : IDraftAutoRepairService
{
    private readonly AppDbContext _db;
    private readonly ILlmProviderAdapter _adapter;
    private readonly LlmProviderService _providerService;
    private readonly PlatformContentPolicy _platformContentPolicy;
    private readonly IDraftValidatorResolver _validatorResolver;
    private readonly ILogger<DraftAutoRepairService> _logger;

    public DraftAutoRepairService(
        AppDbContext db,
        ILlmProviderAdapter adapter,
        LlmProviderService providerService,
        PlatformContentPolicy platformContentPolicy,
        IDraftValidatorResolver validatorResolver,
        ILogger<DraftAutoRepairService> logger)
    {
        _db = db;
        _adapter = adapter;
        _providerService = providerService;
        _platformContentPolicy = platformContentPolicy;
        _validatorResolver = validatorResolver;
        _logger = logger;
    }

    public async Task<DraftRepairResult> RepairPlatformThreadAsync(
        PlatformThread thread,
        string validationError,
        Guid userId,
        CancellationToken ct)
    {
        var draft = thread.Draft;
        if (draft == null && thread.DraftId != Guid.Empty)
        {
            draft = await _db.Drafts
                .Include(d => d.Segments)
                .FirstOrDefaultAsync(d => d.Id == thread.DraftId, ct);
        }

        var currentContent = !string.IsNullOrWhiteSpace(thread.Content)
            ? thread.Content
            : draft?.Content ?? string.Empty;

        var repairResult = await ExecuteRepairLoopAsync(
            thread.Platform,
            currentContent,
            validationError,
            userId,
            draft?.Id,
            ct);

        if (!repairResult.Success || string.IsNullOrEmpty(repairResult.RepairedContent))
        {
            return repairResult;
        }

        thread.Content = repairResult.RepairedContent;
        thread.UpdatedAt = DateTime.UtcNow;

        if (draft != null)
        {
            await ApplyRepairedContentToDraftAsync(draft, repairResult.RepairedContent, ct);
        }

        await _db.SaveChangesAsync(ct);
        return repairResult;
    }

    public async Task<DraftRepairResult> RepairDraftAsync(
        Draft draft,
        string platform,
        string validationError,
        Guid userId,
        CancellationToken ct)
    {
        var currentContent = draft.Content ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentContent))
        {
            var segments = await _db.ThreadSegments
                .Where(s => s.DraftId == draft.Id)
                .OrderBy(s => s.Position)
                .Select(s => s.Content)
                .ToListAsync(ct);
            currentContent = string.Join("\n\n---\n\n", segments);
        }

        var repairResult = await ExecuteRepairLoopAsync(
            platform,
            currentContent,
            validationError,
            userId,
            draft.Id,
            ct);

        if (!repairResult.Success || string.IsNullOrEmpty(repairResult.RepairedContent))
        {
            return repairResult;
        }

        await ApplyRepairedContentToDraftAsync(draft, repairResult.RepairedContent, ct);

        var thread = await _db.PlatformThreads
            .FirstOrDefaultAsync(t => t.DraftId == draft.Id && t.Platform == platform, ct);
        if (thread != null)
        {
            thread.Content = repairResult.RepairedContent;
            thread.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return repairResult;
    }

    private async Task<DraftRepairResult> ExecuteRepairLoopAsync(
        string platformString,
        string originalContent,
        string validationError,
        Guid userId,
        Guid? draftId,
        CancellationToken ct)
    {
        if (!SocialPlatformParser.TryParse(platformString, out var platform))
        {
            return DraftRepairResult.Failed($"Invalid platform: {platformString}");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
        var provider = user != null ? await _providerService.GetProviderForUserAsync(_db, user, ct) : null;
        provider ??= await _db.LlmProviders.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, ct)
                  ?? await _db.LlmProviders.FirstOrDefaultAsync(p => p.IsActive, ct);

        if (provider == null)
        {
            return DraftRepairResult.Failed("No active LLM provider configured.");
        }

        var credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);
        var rules = _platformContentPolicy.GetAdaptationRules(platform);
        var validator = _validatorResolver.Resolve(platform);

        var mediaAssets = draftId.HasValue
            ? await _db.MediaAssets.Where(m => m.DraftId == draftId.Value).ToListAsync(ct)
            : new List<MediaAsset>();

        var currentValidationError = validationError;
        var contentToRepair = originalContent;
        const int maxAttempts = 2;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine($"You are an AI assistant correcting a social media thread that failed validation for platform: {platform}.");
            prompt.AppendLine($"\nPlatform Rules:\n{rules}");
            prompt.AppendLine($"\nValidation Error:\n{currentValidationError}");
            prompt.AppendLine($"\nOriginal Content:\n{contentToRepair}");
            prompt.AppendLine("\nTASK: Revise the draft so that it strictly adheres to all character limits and platform constraints, completely eliminating the validation error.");
            prompt.AppendLine("Preserve all key facts, narrative, tone, and source links.");
            prompt.AppendLine("IMPORTANT: Return ONLY the revised content in markdown format. Use --- on separate lines to separate thread segments. Do not include any explanations, preamble, or markdown code fences.");

            var request = new OpenAiModels.ChatCompletionRequest
            {
                Model = credentials.Model,
                Messages = new List<OpenAiModels.OpenAiMessage>
                {
                    new() { Role = "system", Content = "You are a social media content adaptation and validation repair assistant. Return only clean markdown content." },
                    new() { Role = "user", Content = prompt.ToString() }
                },
                Stream = false
            };

            string responseText;
            try
            {
                var completion = await _adapter.CompleteAsync(request, credentials, ct);
                responseText = completion?.Choices?.FirstOrDefault()?.Message.Content?.ToString()?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM repair call failed on attempt {Attempt} for platform {Platform}", attempt, platform);
                return DraftRepairResult.Failed($"LLM repair call failed: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(responseText))
            {
                _logger.LogWarning("LLM returned empty content on repair attempt {Attempt} for platform {Platform}", attempt, platform);
                continue;
            }

            responseText = StripCodeFences(responseText);

            var report = validator.Validate(responseText, mediaAssets);
            if (!report.HasBlockingErrors)
            {
                _logger.LogInformation("Draft was successfully repaired on attempt {Attempt} for platform {Platform}", attempt, platform);
                return DraftRepairResult.Succeeded(responseText);
            }

            var nextError = report.Posts
                .SelectMany(p => p.Issues)
                .FirstOrDefault(i => i.Severity == ValidateDraftSeverity.Error)?.Message ?? "Validation failed.";

            _logger.LogWarning("Repaired draft on attempt {Attempt} still has validation error: {Error}", attempt, nextError);
            currentValidationError = nextError;
            contentToRepair = responseText;
        }

        return DraftRepairResult.Failed($"Draft repair failed after {maxAttempts} attempts: {currentValidationError}");
    }

    private static string StripCodeFences(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```markdown", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring("```markdown".Length);
        }
        else if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(3);
        }

        if (trimmed.EndsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 3);
        }

        return trimmed.Trim();
    }

    private async Task ApplyRepairedContentToDraftAsync(Draft draft, string repairedContent, CancellationToken ct)
    {
        draft.Content = repairedContent;
        draft.UpdatedAt = DateTime.UtcNow;

        var existingSegments = await _db.ThreadSegments
            .Where(s => s.DraftId == draft.Id)
            .ToListAsync(ct);
        _db.ThreadSegments.RemoveRange(existingSegments);

        var segments = DraftSegmentService.SplitMarkdownIntoSegments(repairedContent);
        for (int i = 0; i < segments.Count; i++)
        {
            _db.ThreadSegments.Add(new ThreadSegment
            {
                DraftId = draft.Id,
                Position = i,
                Content = segments[i]
            });
        }
    }
}
