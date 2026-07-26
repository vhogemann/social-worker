using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record GeneratePlatformVariantsArgs(string CanonicalDraftId, List<string> Platforms);

public sealed record GeneratedPlatformVariant(string Platform, Guid DraftId, IReadOnlyList<string>? Warnings = null);

public sealed record GeneratePlatformVariantsResult(
    bool Success,
    IReadOnlyList<GeneratedPlatformVariant> CreatedVariants,
    IReadOnlyList<string> Issues,
    string Message,
    string? Error = null) : IChatToolResult
{
    public static implicit operator string(GeneratePlatformVariantsResult result) => result.ToDisplayText();

    public string ToDisplayText()
    {
        return Message;
    }
}

public sealed class GeneratePlatformVariantsTool : ChatToolBase<GeneratePlatformVariantsArgs, GeneratePlatformVariantsResult>
{
    private readonly AppDbContext _db;
    private readonly ILlmProviderAdapter _adapter;
    private readonly DraftsService _draftsService;
    private readonly LlmProviderService _providerService;
    private readonly PlatformContentPolicy _platformContentPolicy;

    public GeneratePlatformVariantsTool(
        AppDbContext db,
        ILlmProviderAdapter adapter,
        DraftsService draftsService,
        LlmProviderService providerService,
        PlatformContentPolicy platformContentPolicy)
    {
        _db = db;
        _adapter = adapter;
        _draftsService = draftsService;
        _providerService = providerService;
        _platformContentPolicy = platformContentPolicy;
    }

    public override string Name => "generate_platform_variants";
    public override string Description => "Generate platform-specific adaptations of the current draft for other social networks. The LLM will restructure content per-platform constraints (character limits, tone, format).";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "canonicalDraftId": {
              "type": "string",
              "description": "The UUID of the canonical draft to adapt."
            },
            "platforms": {
              "type": "array",
              "items": { "type": "string", "enum": ["Bluesky", "Twitter", "LinkedIn", "Facebook", "Instagram"] },
              "description": "Target platforms to generate variants for."
            }
          },
          "required": ["canonicalDraftId", "platforms"]
        }
        """).RootElement.Clone();

    public override async Task<GeneratePlatformVariantsResult> ExecuteAsync(GeneratePlatformVariantsArgs args, Models.ToolExecutionContext context)
    {
        if (!Guid.TryParse(args.CanonicalDraftId, out var canonicalGuid))
        {
            return new GeneratePlatformVariantsResult(false, Array.Empty<GeneratedPlatformVariant>(), Array.Empty<string>(), "Error: Invalid canonical draft ID.", "Invalid canonical draft ID.");
        }

        var canonical = await _db.Drafts
            .FirstOrDefaultAsync(d => d.Id == canonicalGuid && d.UserId == context.UserId && d.Status != DraftStatus.Deleted, context.CancellationToken);
        if (canonical == null)
        {
            return new GeneratePlatformVariantsResult(false, Array.Empty<GeneratedPlatformVariant>(), Array.Empty<string>(), "Error: Canonical draft not found or access denied.", "Canonical draft not found or access denied.");
        }

        var sourcePlatform = canonical.TargetPlatform?.ToString() ?? "Bluesky";
        var createdVariants = new List<GeneratedPlatformVariant>();
        var errors = new List<string>();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == context.UserId && u.IsActive, context.CancellationToken);
        var provider = user != null ? await _providerService.GetProviderForUserAsync(_db, user, context.CancellationToken) : null;

        if (provider == null)
        {
            return new GeneratePlatformVariantsResult(false, Array.Empty<GeneratedPlatformVariant>(), Array.Empty<string>(), "Error: No active LLM provider configured.", "No active LLM provider configured.");
        }

        var credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);

        foreach (var platform in args.Platforms)
        {
            if (!Enum.TryParse<SocialPlatform>(platform, true, out var targetPlatform))
            {
                errors.Add($"Invalid platform: {platform}");
                continue;
            }

            if (targetPlatform == canonical.TargetPlatform)
            {
                errors.Add($"Skipping {platform}: same as source platform ({sourcePlatform})");
                continue;
            }

            var existing = await _db.Drafts.AnyAsync(d =>
                d.CanonicalDraftId == canonicalGuid &&
                d.TargetPlatform == targetPlatform &&
                d.Status != DraftStatus.Deleted, context.CancellationToken);
            if (existing)
            {
                errors.Add($"Skipping {platform}: variant already exists");
                continue;
            }

            var platformRules = _platformContentPolicy.GetAdaptationRules(targetPlatform);
            var prompt = new StringBuilder();
            prompt.AppendLine($"You are adapting content from {sourcePlatform} to {targetPlatform}.");
            prompt.AppendLine($"\n{platformRules}");
            prompt.AppendLine($"\nOriginal content ({sourcePlatform}):");
            prompt.AppendLine(canonical.Content ?? "");
            prompt.AppendLine("\nIMPORTANT: Return ONLY the adapted content in markdown format. Use --- on separate lines to separate thread segments. Do not include any explanation, preamble, or postamble.");

            var request = new OpenAiModels.ChatCompletionRequest
            {
                Model = credentials.Model,
                Messages = new List<OpenAiModels.OpenAiMessage>
                {
                    new() { Role = "system", Content = "You are a social media content adaptation assistant. Adapt content faithfully while respecting platform constraints." },
                    new() { Role = "user", Content = prompt.ToString() }
                },
                Stream = false
            };

            string adaptedContent;
            try
            {
                var response = await _adapter.CompleteAsync(request, credentials, context.CancellationToken);
                adaptedContent = response?.Choices?.FirstOrDefault()?.Message.Content?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(adaptedContent))
                {
                    errors.Add($"{platform}: LLM returned empty content");
                    continue;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{platform}: LLM call failed - {ex.Message}");
                continue;
            }

            var policyResult = _platformContentPolicy.Evaluate(targetPlatform, adaptedContent, normalizeFormatting: true);
            if (!policyResult.IsValid)
            {
                errors.Add($"{platform}: validation failed - {string.Join(" | ", policyResult.Errors)}");
                continue;
            }

            adaptedContent = policyResult.NormalizedContent;

            var variant = new Draft
            {
                Title = $"{canonical.Title} ({platform})",
                Content = adaptedContent,
                UserId = context.UserId,
                TargetPlatform = targetPlatform,
                CanonicalDraftId = canonicalGuid,
                Status = DraftStatus.Editing
            };
            _db.Drafts.Add(variant);
            await _db.SaveChangesAsync(context.CancellationToken);

            var thread = new PlatformThread
            {
                DraftId = variant.Id,
                Platform = platform,
                Stage = PlatformThreadStage.Draft,
                Content = adaptedContent
            };
            _db.PlatformThreads.Add(thread);
            await _db.SaveChangesAsync(context.CancellationToken);

            await _draftsService.ReconcileSegmentsAsync(variant, adaptedContent, context.CancellationToken);
            await _db.SaveChangesAsync(context.CancellationToken);

            if (policyResult.Warnings.Count > 0)
            {
                createdVariants.Add(new GeneratedPlatformVariant(platform, variant.Id, policyResult.Warnings));
            }
            else
            {
                createdVariants.Add(new GeneratedPlatformVariant(platform, variant.Id));
            }
        }

        canonical.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(context.CancellationToken);

        var result = new StringBuilder();
        if (createdVariants.Count > 0)
        {
            var descriptions = createdVariants.Select(v =>
                v.Warnings is { Count: > 0 }
                    ? $"{v.Platform} (ID: {v.DraftId}, warnings: {string.Join(" | ", v.Warnings)})"
                    : $"{v.Platform} (ID: {v.DraftId})");
            result.AppendLine($"Created {createdVariants.Count} variant(s): {string.Join(", ", descriptions)}.");
        }
        if (errors.Count > 0)
        {
            result.AppendLine($"Issues: {string.Join("; ", errors)}.");
        }

        var message = result.Length > 0 ? result.ToString().Trim() : "No variants were created.";
        return new GeneratePlatformVariantsResult(createdVariants.Count > 0, createdVariants, errors, message);
    }

}