using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record ValidateDraftArgs(string? Content);

public sealed class ValidateDraftTool : ChatToolBase<ValidateDraftArgs, string>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly Regex MediaRegex = new(@"!\[(.*?)\]\(media://([0-9a-fA-F\-]{36})\)", RegexOptions.Compiled);

    public ValidateDraftTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override string Name => "validate_draft";
    public override string Description => "Validates the draft's formatting compliance for Bluesky (character limits, image counts, YouTube embeds, and missing ALT texts).";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "content": {
              "type": "string",
              "description": "Optional raw markdown content to validate. If omitted, validates the active draft content from the database."
            }
          }
        }
        """).RootElement.Clone();

    public override async Task<string> ExecuteAsync(ValidateDraftArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        string validationContent = "";
        var mediaAssets = new List<MediaAsset>();

        if (!string.IsNullOrWhiteSpace(args.Content))
        {
            validationContent = args.Content;
            if (draftId.HasValue)
            {
                mediaAssets = await db.MediaAssets.Where(m => m.DraftId == draftId.Value).ToListAsync(ct);
            }
        }
        else
        {
            if (!draftId.HasValue)
            {
                return "Error: No draft ID active.";
            }

            var draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId.Value && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
            if (draft == null)
            {
                return "Error: Draft not found or access denied.";
            }

            validationContent = draft.Content ?? "";
            mediaAssets = await db.MediaAssets.Where(m => m.DraftId == draftId.Value).ToListAsync(ct);
        }

        var segments = DraftsService.SplitMarkdownIntoSegments(validationContent);

        var sb = new StringBuilder();
        sb.AppendLine("### Draft Validation Report");
        sb.AppendLine();

        bool hasErrors = false;
        bool hasWarnings = false;

        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            sb.AppendLine($"#### Post {i + 1}:");

            var imageMatches = MediaRegex.Matches(segment);
            int imageCount = imageMatches.Count;
            var missingAltImages = new List<string>();

            foreach (Match match in imageMatches)
            {
                var markdownAlt = match.Groups[1].Value;
                var mediaIdStr = match.Groups[2].Value;

                if (Guid.TryParse(mediaIdStr, out var mediaId))
                {
                    var asset = mediaAssets.FirstOrDefault(m => m.Id == mediaId);
                    var assetAlt = asset?.AltText;
                    
                    if (string.IsNullOrWhiteSpace(markdownAlt) && string.IsNullOrWhiteSpace(assetAlt))
                    {
                        missingAltImages.Add(asset?.FileName ?? $"media://{mediaId}");
                    }
                }
            }

            bool hasYouTube = segment.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
                              segment.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);

            var cleanedText = MediaRegex.Replace(segment, "").Trim();
            int charCount = cleanedText.Length;

            sb.AppendLine($"- **Character Count**: {charCount} characters (max 300)");

            if (charCount > 300)
            {
                sb.AppendLine($"- ❌ **Error**: Exceeds the 300-character limit by {charCount - 300} characters.");
                hasErrors = true;
            }

            if (imageCount > 4)
            {
                sb.AppendLine($"- ❌ **Error**: Contains {imageCount} images (Bluesky allows a maximum of 4 images per post).");
                hasErrors = true;
            }

            if (imageCount > 0 && hasYouTube)
            {
                sb.AppendLine("- ❌ **Error**: Cannot mix images and YouTube embeds in a single post on Bluesky.");
                hasErrors = true;
            }

            if (missingAltImages.Count > 0)
            {
                sb.AppendLine($"- ⚠️ **Warning**: Missing ALT text on images: {string.Join(", ", missingAltImages)}");
                hasWarnings = true;
            }

            if (charCount <= 300 && imageCount <= 4 && !(imageCount > 0 && hasYouTube) && missingAltImages.Count == 0)
            {
                sb.AppendLine("-   **Status**: Valid");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        if (hasErrors)
        {
            sb.AppendLine("❌ **Overall Status**: Validation failed. Please fix the errors listed above before publishing.");
        }
        else if (hasWarnings)
        {
            sb.AppendLine("⚠️ **Overall Status**: Ready with warnings. (Missing alt text is allowed but highly discouraged).");
        }
        else
        {
            sb.AppendLine("✅ **Overall Status**: Valid. The draft complies with all platform formatting guidelines.");
        }

        return sb.ToString();
    }
}
