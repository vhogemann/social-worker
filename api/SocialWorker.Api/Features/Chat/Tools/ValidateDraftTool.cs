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
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record ValidateDraftArgs(string? Content);

public enum ValidateDraftSeverity
{
    Error,
    Warning
}

public sealed record ValidateDraftIssue(ValidateDraftSeverity Severity, string Message);

public sealed record ValidateDraftPostResult(
    int PostNumber,
    int CharacterCount,
    int ImageCount,
    bool HasYouTube,
    IReadOnlyList<ValidateDraftIssue> Issues)
{
    public bool HasErrors => Issues.Any(i => i.Severity == ValidateDraftSeverity.Error);
    public bool HasWarnings => Issues.Any(i => i.Severity == ValidateDraftSeverity.Warning);
    public bool IsValid => !HasErrors && !HasWarnings;
}

public enum ValidateDraftOverallStatus
{
    Valid,
    Warnings,
    Failed
}

public sealed record ValidateDraftResult(
    IReadOnlyList<ValidateDraftPostResult> Posts,
    ValidateDraftOverallStatus OverallStatus) : IChatBlockingValidationResult
{
    public bool HasBlockingErrors => OverallStatus == ValidateDraftOverallStatus.Failed;

    public string ToDisplayText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("### Draft Validation Report");
        sb.AppendLine();

        foreach (var post in Posts)
        {
            sb.AppendLine($"#### Post {post.PostNumber}:");
            sb.AppendLine($"- **Character Count**: {post.CharacterCount} characters (max 300)");

            foreach (var issue in post.Issues)
            {
                if (issue.Severity == ValidateDraftSeverity.Error)
                {
                    sb.AppendLine($"- ❌ **Error**: {issue.Message}");
                    continue;
                }

                sb.AppendLine($"- ⚠️ **Warning**: {issue.Message}");
            }

            if (post.IsValid)
            {
                sb.AppendLine("-   **Status**: Valid");
            }

            sb.AppendLine();
        }

        sb.AppendLine("---");
        if (OverallStatus == ValidateDraftOverallStatus.Failed)
        {
            sb.AppendLine("❌ **Overall Status**: Validation failed. Please fix the errors listed above before publishing.");
        }
        else if (OverallStatus == ValidateDraftOverallStatus.Warnings)
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

public sealed class ValidateDraftTool : ChatToolBase<ValidateDraftArgs, ValidateDraftResult>
{
    private static readonly Regex TitleLikeOpenerRegex = new(@"(?i)\b(key\s+takeaways|takeaways|summary|overview|highlights)\b", RegexOptions.Compiled);
    private static readonly Regex PlaceholderLinkTokenRegex = new(@"\[(?=[^\]\n]{0,80}(?i:link|source|youtube|docs))[^\]\n]+\](?!\()", RegexOptions.Compiled);
    private static readonly Regex PlaceholderMediaTokenRegex = new(@"(?i)media://\s*(guid|\{guid\}|placeholder)", RegexOptions.Compiled);
    private static readonly Regex PlaceholderUrlRegex = new(@"(?i)https?://(www\.)?example\.com\b", RegexOptions.Compiled);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BlueskyContentValidator _blueskyContentValidator;

    public ValidateDraftTool(IServiceScopeFactory scopeFactory, BlueskyContentValidator blueskyContentValidator)
    {
        _scopeFactory = scopeFactory;
        _blueskyContentValidator = blueskyContentValidator;
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

    public override async Task<ValidateDraftResult> ExecuteAsync(ValidateDraftArgs args, Guid? draftId, Guid userId, CancellationToken ct)
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
                return new ValidateDraftResult(
                    new[]
                    {
                        new ValidateDraftPostResult(
                            1,
                            0,
                            0,
                            false,
                            new[] { new ValidateDraftIssue(ValidateDraftSeverity.Error, "No draft ID active.") })
                    },
                    ValidateDraftOverallStatus.Failed);
            }

            var draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId.Value && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
            if (draft == null)
            {
                return new ValidateDraftResult(
                    new[]
                    {
                        new ValidateDraftPostResult(
                            1,
                            0,
                            0,
                            false,
                            new[] { new ValidateDraftIssue(ValidateDraftSeverity.Error, "Draft not found or access denied.") })
                    },
                    ValidateDraftOverallStatus.Failed);
            }

            validationContent = draft.Content ?? "";
            mediaAssets = await db.MediaAssets.Where(m => m.DraftId == draftId.Value).ToListAsync(ct);
        }

        var segments = _blueskyContentValidator.Analyze(validationContent);

        bool hasErrors = false;
        bool hasWarnings = false;
        var posts = new List<ValidateDraftPostResult>();

        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var issues = new List<ValidateDraftIssue>();

            var imageReferences = SharedPatterns.ExtractMediaReferences(segment.Segment);
            int imageCount = segment.ImageCount;
            var missingAltImages = new List<string>();

            foreach (var mediaRef in imageReferences)
            {
                var markdownAlt = mediaRef.AltText;
                var mediaId = mediaRef.MediaId;

                var asset = mediaAssets.FirstOrDefault(m => m.Id == mediaId);
                var assetAlt = asset?.AltText;

                if (string.IsNullOrWhiteSpace(markdownAlt) && string.IsNullOrWhiteSpace(assetAlt))
                {
                    missingAltImages.Add(asset?.FileName ?? $"media://{mediaId}");
                }
            }

            bool hasYouTube = segment.HasYouTube;

            bool hasUnsupportedMarkdown = segment.HasUnsupportedMarkdown;
            var firstNonEmptyLine = segment.Segment
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Select(l => l.Trim())
                .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l)) ?? string.Empty;
            bool hasTitleLikeOpener = firstNonEmptyLine.Length > 0 &&
                                      (firstNonEmptyLine.EndsWith(':') || TitleLikeOpenerRegex.IsMatch(firstNonEmptyLine));
            bool hasPlaceholderLinks = PlaceholderLinkTokenRegex.IsMatch(segment.Segment);
            bool hasPlaceholderMedia = PlaceholderMediaTokenRegex.IsMatch(segment.Segment);
            bool hasPlaceholderUrls = PlaceholderUrlRegex.IsMatch(segment.Segment);

            int charCount = segment.CharacterCount;

            if (charCount > 300)
            {
                issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Error, $"Exceeds the 300-character limit by {charCount - 300} characters."));
                hasErrors = true;
            }

            if (imageCount > 4)
            {
                issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Error, $"Contains {imageCount} images (Bluesky allows a maximum of 4 images per post)."));
                hasErrors = true;
            }

            if (imageCount > 0 && hasYouTube)
            {
                issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Error, "Cannot mix images and YouTube embeds in a single post on Bluesky."));
                hasErrors = true;
            }

            if (hasUnsupportedMarkdown)
            {
                issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Error, "Unsupported markdown styling detected for Bluesky (bold/italic/heading markers). Use plain text formatting."));
                hasErrors = true;
            }

            if (hasPlaceholderLinks)
            {
                issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Error, "Placeholder link text detected (e.g., [source link]). Use concrete URLs or valid markdown links."));
                hasErrors = true;
            }

            if (hasPlaceholderMedia)
            {
                issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Error, "Placeholder media reference detected (e.g., media://guid). Use a real media://{guid} from add_image_source or render_code_blocks."));
                hasErrors = true;
            }

            if (hasPlaceholderUrls)
            {
                issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Error, "Placeholder URL detected (e.g., example.com). Use a concrete source URL."));
                hasErrors = true;
            }

            if (missingAltImages.Count > 0)
            {
                issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Warning, $"Missing ALT text on images: {string.Join(", ", missingAltImages)}"));
                hasWarnings = true;
            }

            if (hasTitleLikeOpener)
            {
                issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Warning, "Title-like opener detected. Prefer a conversational opening line for Bluesky."));
                hasWarnings = true;
            }

            posts.Add(new ValidateDraftPostResult(i + 1, charCount, imageCount, hasYouTube, issues));
        }

        var overallStatus = ValidateDraftOverallStatus.Valid;
        if (hasErrors)
        {
            overallStatus = ValidateDraftOverallStatus.Failed;
        }
        else if (hasWarnings)
        {
            overallStatus = ValidateDraftOverallStatus.Warnings;
        }

        return new ValidateDraftResult(posts, overallStatus);
    }
}
