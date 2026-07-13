using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record FormatValidatePlatformContentArgs(string Platform, string Content, bool NormalizeFormatting = true);

public sealed record FormatValidatePlatformContentResult(
    string Platform,
    bool IsValid,
    string Content,
    string NormalizedContent,
    List<string> Errors,
    List<string> Warnings);

public sealed class FormatValidatePlatformContentTool : ChatToolBase<FormatValidatePlatformContentArgs, FormatValidatePlatformContentResult>
{
    private readonly PlatformContentPolicy _platformContentPolicy;

    public FormatValidatePlatformContentTool(PlatformContentPolicy platformContentPolicy)
    {
        _platformContentPolicy = platformContentPolicy;
    }

    public override string Name => "format_validate_platform_content";
    public override string Description => "Formats and validates a draft content block against target platform constraints. Use this before saving or publishing platform variants.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "platform": {
              "type": "string",
              "enum": ["Bluesky", "Twitter", "LinkedIn", "Facebook", "Instagram"],
              "description": "Target platform to validate against."
            },
            "content": {
              "type": "string",
              "description": "Candidate post/thread content to validate and optionally normalize."
            },
            "normalizeFormatting": {
              "type": "boolean",
              "description": "When true, removes markdown styling that platforms usually do not render."
            }
          },
          "required": ["platform", "content"]
        }
        """).RootElement.Clone();

    public override Task<FormatValidatePlatformContentResult> ExecuteAsync(FormatValidatePlatformContentArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (!Enum.TryParse<SocialPlatform>(args.Platform, true, out var platform))
        {
            return Task.FromResult(new FormatValidatePlatformContentResult(
                args.Platform,
                false,
                args.Content,
                args.Content,
                new List<string> { $"Invalid platform: {args.Platform}" },
                new List<string>()));
        }

        var result = _platformContentPolicy.Evaluate(platform, args.Content ?? string.Empty, args.NormalizeFormatting);
        return Task.FromResult(new FormatValidatePlatformContentResult(
            platform.ToString(),
            result.IsValid,
            args.Content ?? string.Empty,
            result.NormalizedContent,
            result.Errors,
            result.Warnings));
    }
}