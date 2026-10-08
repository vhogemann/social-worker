using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Validation;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record ValidateDraftArgs(string? Content, string? Platform = null);

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
            sb.AppendLine($"- **Character Count**: {post.CharacterCount} characters");

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
    private readonly AppDbContext _db;
    private readonly IDraftValidatorResolver _validatorResolver;

    public ValidateDraftTool(AppDbContext db, IDraftValidatorResolver validatorResolver)
    {
        _db = db;
        _validatorResolver = validatorResolver;
    }

    public override string Name => "validate_draft";
    public override string Description => "Validates content compliance against draft formatting rules. For canonical drafts, checks structure, citation syntax, and placeholders without microblogging character limits. For platform variants, validates against that platform's rules.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "content": {
              "type": "string",
              "description": "Optional raw markdown content to validate. If omitted, validates the active draft content from the database."
            },
            "platform": {
              "type": "string",
              "description": "Optional target platform name (e.g., 'Bluesky', 'LinkedIn', 'Twitter') to validate against. If omitted, uses the draft's configured platform or canonical rules."
            }
          }
        }
        """).RootElement.Clone();

    public override async Task<ValidateDraftResult> ExecuteAsync(ValidateDraftArgs args, Models.ToolExecutionContext context)
    {
        string validationContent = "";
        var mediaAssets = new List<MediaAsset>();
        SocialPlatform? targetPlatform = null;

        if (!string.IsNullOrWhiteSpace(args.Platform) &&
            SocialWorker.Api.Infrastructure.Platforms.SocialPlatformParser.TryParse(args.Platform, out var explicitPlatform))
        {
            targetPlatform = explicitPlatform;
        }

        if (!string.IsNullOrWhiteSpace(args.Content))
        {
            validationContent = args.Content;
            if (context.DraftId.HasValue)
            {
                var draft = await _db.Drafts.FirstOrDefaultAsync(
                    d => d.Id == context.DraftId.Value && d.UserId == context.UserId && d.Status != DraftStatus.Deleted,
                    context.CancellationToken);
                
                if (draft != null && !targetPlatform.HasValue)
                {
                    targetPlatform = draft.TargetPlatform;
                }

                mediaAssets = await _db.MediaAssets
                    .Where(m => m.DraftId == context.DraftId.Value)
                    .ToListAsync(context.CancellationToken);
            }
        }
        else
        {
            if (!context.DraftId.HasValue)
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

            var draft = await _db.Drafts.FirstOrDefaultAsync(
                d => d.Id == context.DraftId.Value && d.UserId == context.UserId && d.Status != DraftStatus.Deleted,
                context.CancellationToken);

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

            if (!targetPlatform.HasValue)
            {
                targetPlatform = draft.TargetPlatform;
            }

            validationContent = draft.Content ?? "";
            mediaAssets = await _db.MediaAssets
                .Where(m => m.DraftId == context.DraftId.Value)
                .ToListAsync(context.CancellationToken);
        }

        var validator = _validatorResolver.Resolve(targetPlatform);
        return validator.Validate(validationContent, mediaAssets);
    }
}
