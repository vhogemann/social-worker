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

namespace SocialWorker.Api.Features.Chat.Tools;

public record PublishPlatformArgs(string Platform);

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
    private readonly IServiceScopeFactory _scopeFactory;

    public PublishPlatformTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override string Name => "publish";
    public override string Description => "Triggers the publication of a drafted thread to a target platform. This is only allowed when the draft's platform variant is not already Sent.";
    public override JsonElement Parameters => JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "platform": {
                "type": "string",
                "description": "The platform to publish to (e.g. 'Bluesky', 'Twitter')."
            }
        },
        "required": ["platform"]
    }
    """).RootElement;

    public override async Task<PublishPlatformToolResult> ExecuteAsync(PublishPlatformArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (draftId == null)
            return new PublishPlatformToolResult(false, "No active draft context.", Error: "No active draft context.");

        string platform = args.Platform ?? "";

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisherResolver = scope.ServiceProvider.GetRequiredService<IPublisherResolver>();
        var blueskyContentValidator = scope.ServiceProvider.GetRequiredService<BlueskyContentValidator>();
        var replyTargetResolver = scope.ServiceProvider.GetRequiredService<IBlueskyReplyTargetResolver>();

        var thread = await db.PlatformThreads
            .FirstOrDefaultAsync(t => t.DraftId == draftId && t.Platform.ToLower() == platform.ToLower(), ct);

        if (thread == null)
        {
            return new PublishPlatformToolResult(false, $"No platform thread found for platform '{platform}' in this draft.", Error: $"No platform thread found for platform '{platform}' in this draft.");
        }

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId && a.Platform == platform, ct);
        if (account == null)
        {
            return new PublishPlatformToolResult(false, $"No connected account found for platform: {platform}", Error: $"No connected account found for platform: {platform}");
        }

        var publisher = publisherResolver.Resolve(platform);
        if (publisher == null)
        {
            return new PublishPlatformToolResult(false, $"No publisher configured for platform: {platform}", Error: $"No publisher configured for platform: {platform}");
        }

        // Validate content before publishing
        var validationError = ValidateThreadContent(thread.Content ?? "", thread.Platform, blueskyContentValidator);
        if (validationError != null)
        {
            return new PublishPlatformToolResult(false, validationError, Error: validationError);
        }

        var replyTargetValidationError = await ValidateBlueskyReplyTargetAsync(db, replyTargetResolver, thread, ct);
        if (replyTargetValidationError != null)
        {
            return new PublishPlatformToolResult(false, replyTargetValidationError, Error: replyTargetValidationError);
        }

        var result = await publisher.PublishAsync(thread, account, ct);

        if (result.Success)
        {
            // Clear any previous posts from this platform thread (in case of republication)
            var oldPosts = await db.Posts.Where(p => p.PlatformThreadId == thread.Id).ToListAsync(ct);
            db.Posts.RemoveRange(oldPosts);
            
            foreach (var publishedPost in result.Posts)
            {
                var post = new Post
                {
                    DraftId = draftId.Value,
                    PlatformThreadId = thread.Id,
                    SegmentIndex = publishedPost.SegmentIndex,
                    Platform = thread.Platform,
                    RemoteId = publishedPost.RemoteId,
                    Url = publishedPost.Url
                };
                db.Posts.Add(post);
            }
            
            thread.Stage = PlatformThreadStage.Sent;
            thread.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            
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

    private static string? ValidateThreadContent(string content, string platform, BlueskyContentValidator blueskyContentValidator)
    {
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
