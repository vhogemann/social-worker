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

public sealed class GeneratePlatformVariantsTool : ChatToolBase<GeneratePlatformVariantsArgs, string>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LlmProviderService _providerService;

    public GeneratePlatformVariantsTool(IServiceScopeFactory scopeFactory, LlmProviderService providerService)
    {
        _scopeFactory = scopeFactory;
        _providerService = providerService;
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

    public override async Task<string> ExecuteAsync(GeneratePlatformVariantsArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (!Guid.TryParse(args.CanonicalDraftId, out var canonicalGuid))
        {
            return "Error: Invalid canonical draft ID.";
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var adapter = scope.ServiceProvider.GetRequiredService<ILlmProviderAdapter>();
        var draftService = scope.ServiceProvider.GetRequiredService<DraftsService>();

        var canonical = await db.Drafts
            .FirstOrDefaultAsync(d => d.Id == canonicalGuid && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
        if (canonical == null)
        {
            return "Error: Canonical draft not found or access denied.";
        }

        var sourcePlatform = canonical.TargetPlatform?.ToString() ?? "Bluesky";
        var createdVariants = new List<string>();
        var errors = new List<string>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
        var provider = user != null ? await _providerService.GetProviderForUserAsync(db, user, ct) : null;

        if (provider == null)
        {
            return "Error: No active LLM provider configured.";
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

            var existing = await db.Drafts.AnyAsync(d =>
                d.CanonicalDraftId == canonicalGuid &&
                d.TargetPlatform == targetPlatform &&
                d.Status != DraftStatus.Deleted, ct);
            if (existing)
            {
                errors.Add($"Skipping {platform}: variant already exists");
                continue;
            }

            var platformRules = GetPlatformRules(platform);
            var prompt = new StringBuilder();
            prompt.AppendLine($"You are adapting content from {sourcePlatform} to {platform}.");
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
                var response = await adapter.CompleteAsync(request, credentials, ct);
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

            var variant = new Draft
            {
                Title = $"{canonical.Title} ({platform})",
                Content = adaptedContent,
                UserId = userId,
                TargetPlatform = targetPlatform,
                CanonicalDraftId = canonicalGuid,
                Status = DraftStatus.Editing
            };
            db.Drafts.Add(variant);
            await db.SaveChangesAsync(ct);

            var thread = new PlatformThread
            {
                DraftId = variant.Id,
                Platform = platform,
                Stage = PlatformThreadStage.Draft,
                Content = adaptedContent
            };
            db.PlatformThreads.Add(thread);
            await db.SaveChangesAsync(ct);

            await draftService.ReconcileSegmentsAsync(variant, adaptedContent, ct);
            await db.SaveChangesAsync(ct);

            createdVariants.Add($"{platform} (ID: {variant.Id})");
        }

        canonical.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var result = new StringBuilder();
        if (createdVariants.Count > 0)
        {
            result.AppendLine($"Created {createdVariants.Count} variant(s): {string.Join(", ", createdVariants)}.");
        }
        if (errors.Count > 0)
        {
            result.AppendLine($"Issues: {string.Join("; ", errors)}.");
        }

        return result.Length > 0 ? result.ToString().Trim() : "No variants were created.";
    }

    private static string GetPlatformRules(string platform)
    {
        return platform.ToLowerInvariant() switch
        {
            "twitter" => """
Twitter rules:
- 280 characters per post maximum, 2-3 posts typical
- Punchy, conversational tone
- Break into short posts, each standalone
- Use hashtags sparingly (max 2)
- Reply threads: connect posts logically
""",
            "linkedin" => """
LinkedIn rules:
- ~3000 characters per post, 1-2 posts
- Professional tone
- Single long-form post or 2-part series
- Emojis used strategically
- Call-to-action at end
""",
            "instagram" => """
Instagram rules:
- 2200 character caption limit, visual-first
- Lifestyle/visual tone, relatable
- Shorter sentences, more emojis
- Hashtags at end (5-10)
- Focus on visual story
""",
            "facebook" => """
Facebook rules:
- No hard character limit, conversational
- Friendly, engaging tone
- Slightly longer form than Twitter
- Multi-generational audience (simpler language)
- Emojis welcome, moderate use
""",
            _ => ""
        };
    }
}