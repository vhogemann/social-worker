using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Validation;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record ValidateDraftArgs(string? Content);

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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BlueskyDraftValidator _blueskyDraftValidator;

    public ValidateDraftTool(IServiceScopeFactory scopeFactory, BlueskyDraftValidator blueskyDraftValidator)
    {
        _scopeFactory = scopeFactory;
        _blueskyDraftValidator = blueskyDraftValidator;
    }

    public override string Name => "validate_draft";
    public override string Description => "Validates content compliance against Bluesky's formatting rules. It checks character limits, image counts, ALT texts, and YouTube embeds, and returns a detailed report of validation issues.";

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
                return BlueskyDraftValidator.CreateFailureResult("No draft ID active.");
            }

            var draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId.Value && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
            if (draft == null)
            {
                return BlueskyDraftValidator.CreateFailureResult("Draft not found or access denied.");
            }

            validationContent = draft.Content ?? "";
            mediaAssets = await db.MediaAssets.Where(m => m.DraftId == draftId.Value).ToListAsync(ct);
        }

        return _blueskyDraftValidator.Validate(validationContent, mediaAssets);
    }
}
