using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure.Platforms;

namespace SocialWorker.Api.Features.Chat.Tools;

public record PublishPlatformArgs(string Platform, Guid? AccountId = null);

public sealed record PublishPlatformToolResult(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("posts")] IReadOnlyList<PublishedPost>? Posts = null,
    [property: JsonPropertyName("error")] string? Error = null,
    [property: JsonPropertyName("authUrl")] string? AuthUrl = null) : IChatToolResult
{
    public static implicit operator string(PublishPlatformToolResult result) => result.ToDisplayText();

    public string ToDisplayText()
    {
        return Success
            ? Message
            : $"Error: {Error ?? Message}";
    }
}

public class PublishPlatformTool : ChatToolBase<PublishPlatformArgs, PublishPlatformToolResult>
{
    private readonly AppDbContext _db;
    private readonly IPublisherResolver _publisherResolver;
    private readonly BlueskyContentValidator _blueskyContentValidator;
    private readonly IBlueskyReplyTargetResolver _replyTargetResolver;
    private readonly IDraftValidatorResolver? _draftValidatorResolver;

    public PublishPlatformTool(
        AppDbContext db,
        IPublisherResolver publisherResolver,
        BlueskyContentValidator blueskyContentValidator,
        IBlueskyReplyTargetResolver replyTargetResolver,
        IDraftValidatorResolver? draftValidatorResolver = null)
    {
        _db = db;
        _publisherResolver = publisherResolver;
        _blueskyContentValidator = blueskyContentValidator;
        _replyTargetResolver = replyTargetResolver;
        _draftValidatorResolver = draftValidatorResolver;
    }

    public override string Name => "publish";
    public override string Description => "Publishes the active saved draft thread to the specified platform. This action will fail if the variant is already Sent/published.";
    public override JsonElement Parameters => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "platform": {
                "type": "string",
                "description": "The platform to publish to (e.g. 'Bluesky', 'Twitter', 'Threads', 'LinkedIn')."
            },
            "accountId": {
                "type": "string",
                "description": "Optional account ID to publish with. Required if the user has multiple accounts for this platform."
            }
        },
        "required": ["platform"]
    }
    """).RootElement;

    public override async Task<PublishPlatformToolResult> ExecuteAsync(PublishPlatformArgs args, Models.ToolExecutionContext context)
    {
        if (context.DraftId == null)
            return new PublishPlatformToolResult(false, "No active draft context.", Error: "No active draft context.");

        string platform = args.Platform;
        var normalizedPlatform = SocialPlatformParser.TryParse(platform, out var parsedPlatform)
            ? parsedPlatform.ToString()
            : platform;

        var thread = await _db.PlatformThreads
            .FirstOrDefaultAsync(t => t.DraftId == context.DraftId &&
                (t.Platform.ToLower() == platform.ToLower() || t.Platform.ToLower() == normalizedPlatform.ToLower()),
                context.CancellationToken);

        if (thread == null)
        {
            var draft = await _db.Drafts.FirstOrDefaultAsync(d => d.Id == context.DraftId && d.UserId == context.UserId, context.CancellationToken);
            if (draft != null && !string.IsNullOrWhiteSpace(draft.Content))
            {
                thread = new PlatformThread
                {
                    DraftId = draft.Id,
                    Platform = normalizedPlatform,
                    Content = draft.Content,
                    Stage = PlatformThreadStage.Draft,
                    UpdatedAt = DateTime.UtcNow
                };

                _db.PlatformThreads.Add(thread);
                await _db.SaveChangesAsync(context.CancellationToken);
            }
            else
            {
                return new PublishPlatformToolResult(false, $"No platform thread found for platform '{platform}' in this draft.", Error: $"No platform thread found for platform '{platform}' in this draft.");
            }
        }

        if (thread.Stage == PlatformThreadStage.Sent)
        {
            var existingPosts = await _db.Posts
                .Where(p => p.PlatformThreadId == thread.Id)
                .OrderBy(p => p.SegmentIndex)
                .ToListAsync(context.CancellationToken);

            var postResults = existingPosts.Select(p => new PublishedPost
            {
                SegmentIndex = p.SegmentIndex,
                RemoteId = p.RemoteId ?? "",
                RemoteCid = p.RemoteCid,
                PublishedText = p.PublishedText,
                Url = p.Url ?? ""
            }).ToList();

            return new PublishPlatformToolResult(
                true,
                $"Thread for platform '{platform}' is already published.",
                Posts: postResults
            );
        }

        Account? account = null;
        var requestedAccountId = args.AccountId ?? thread.AccountId;
        if (requestedAccountId.HasValue)
        {
            account = await _db.Accounts.FirstOrDefaultAsync(
                a => a.Id == requestedAccountId.Value && a.UserId == context.UserId &&
                    (a.Platform.ToLower() == platform.ToLower() || a.Platform.ToLower() == normalizedPlatform.ToLower()),
                context.CancellationToken);

            if (account == null)
            {
                return new PublishPlatformToolResult(false, $"Account '{requestedAccountId.Value}' not found or does not belong to platform '{platform}'.", Error: $"Account '{requestedAccountId.Value}' not found or does not belong to platform '{platform}'.");
            }
        }
        else
        {
            var matchingAccounts = await _db.Accounts
                .Where(a => a.UserId == context.UserId &&
                    (a.Platform.ToLower() == platform.ToLower() || a.Platform.ToLower() == normalizedPlatform.ToLower()))
                .ToListAsync(context.CancellationToken);

            if (matchingAccounts.Count == 0)
            {
                return new PublishPlatformToolResult(false, $"No connected account found for platform: {platform}", Error: $"No connected account found for platform: {platform}");
            }

            if (matchingAccounts.Count > 1)
            {
                var accountListStr = string.Join(", ", matchingAccounts.Select(a => $"{a.Handle ?? a.Id.ToString()} (ID: {a.Id})"));
                return new PublishPlatformToolResult(false, $"Multiple accounts found for platform '{platform}'. Please specify 'accountId': {accountListStr}", Error: $"Multiple accounts found for platform '{platform}'. Please specify 'accountId': {accountListStr}");
            }

            account = matchingAccounts[0];
        }

        thread.AccountId = account.Id;

        var publisher = _publisherResolver.Resolve(normalizedPlatform) ?? _publisherResolver.Resolve(platform);
        if (publisher == null)
        {
            return new PublishPlatformToolResult(false, $"No publisher configured for platform: {platform}", Error: $"No publisher configured for platform: {platform}");
        }

        // Validate content before publishing
        var validationError = await ValidateThreadContentAsync(thread.Content ?? "", thread.Platform, _blueskyContentValidator, _draftValidatorResolver, _db, thread.DraftId, context.CancellationToken);
        if (validationError != null)
        {
            return new PublishPlatformToolResult(false, validationError, Error: validationError);
        }

        var replyTargetValidationError = await ValidateBlueskyReplyTargetAsync(_db, _replyTargetResolver, thread, context.CancellationToken);
        if (replyTargetValidationError != null)
        {
            return new PublishPlatformToolResult(false, replyTargetValidationError, Error: replyTargetValidationError);
        }

        var result = await publisher.PublishAsync(thread, account, context.CancellationToken);

        var outcomeRecorder = new SocialWorker.Api.Features.Publishing.Resilience.PublishOutcomeRecorder(_db);
        await outcomeRecorder.RecordOutcomeAsync(thread, result, context.CancellationToken);

        if (result.Success)
        {
            return new PublishPlatformToolResult(
                true,
                $"Successfully published {result.Posts.Count} segments to {platform}.",
                result.Posts);
        }
        else
        {
            return new PublishPlatformToolResult(
                false,
                result.ErrorMessage ?? "Publishing failed.",
                Error: result.ErrorMessage,
                AuthUrl: result.AuthUrl);
        }
    }

    private static async Task<string?> ValidateThreadContentAsync(
        string content,
        string platform,
        BlueskyContentValidator blueskyContentValidator,
        IDraftValidatorResolver? draftValidatorResolver,
        AppDbContext db,
        Guid draftId,
        CancellationToken ct)
    {
        if (draftValidatorResolver != null && SocialPlatformParser.TryParse(platform, out var socialPlatform))
        {
            var validator = draftValidatorResolver.Resolve(socialPlatform);
            var mediaAssets = await db.MediaAssets.Where(m => m.DraftId == draftId).ToListAsync(ct);
            var result = validator.Validate(content, mediaAssets);
            if (result.OverallStatus == ValidateDraftOverallStatus.Failed)
            {
                var firstError = result.Posts.SelectMany(p => p.Issues).FirstOrDefault(i => i.Severity == ValidateDraftSeverity.Error);
                return firstError?.Message ?? "Validation failed.";
            }
            return null;
        }

        if (!string.Equals(platform, "Bluesky", StringComparison.OrdinalIgnoreCase))
            return null;

        return blueskyContentValidator.GetFirstPublishValidationError(content);
    }

    private static async Task<string?> ValidateBlueskyReplyTargetAsync(
        AppDbContext db,
        IBlueskyReplyTargetResolver resolver,
        PlatformThread thread,
        CancellationToken ct)
    {
        if (!string.Equals(thread.Platform, "Bluesky", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var metadata = await db.DraftBlueskyMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.DraftId == thread.DraftId, ct);

        if (metadata is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(metadata.ReplyParentUrl))
        {
            return "Draft reply target is missing a canonical post URL and cannot be revalidated.";
        }

        var resolved = await resolver.ResolveAsync(metadata.ReplyParentUrl, ct);
        if (!resolved.Success)
        {
            return $"Draft reply target could not be revalidated: {resolved.Error ?? "unknown error"}";
        }

        if (!string.Equals(resolved.ReplyRootUri, metadata.ReplyRootUri, StringComparison.Ordinal)
            || !string.Equals(resolved.ReplyRootCid, metadata.ReplyRootCid, StringComparison.Ordinal)
            || !string.Equals(resolved.ReplyParentUri, metadata.ReplyParentUri, StringComparison.Ordinal)
            || !string.Equals(resolved.ReplyParentCid, metadata.ReplyParentCid, StringComparison.Ordinal))
        {
            return "Draft reply target metadata no longer matches the referenced Bluesky post. Please create a new draft reply target.";
        }

        return null;
    }
}
