using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Publishing.Bluesky;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record SetBlueskyReplyTargetArgs(string Url);

public sealed record SetBlueskyReplyTargetToolResult(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("error")] string? Error = null,
    [property: JsonPropertyName("replyParentUrl")] string? ReplyParentUrl = null,
    [property: JsonPropertyName("replyParentAuthor")] string? ReplyParentAuthor = null,
    [property: JsonPropertyName("replyParentText")] string? ReplyParentText = null,
    [property: JsonPropertyName("replyParentAvatarUrl")] string? ReplyParentAvatarUrl = null) : IChatToolResult
{
    public static implicit operator string(SetBlueskyReplyTargetToolResult result) => result.ToDisplayText();

    public string ToDisplayText()
    {
        return Success
            ? Message
            : $"Error: {Error ?? Message}";
    }
}

public sealed class SetBlueskyReplyTargetTool : ChatToolBase<SetBlueskyReplyTargetArgs, SetBlueskyReplyTargetToolResult>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SetBlueskyReplyTargetTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override string Name => "set_bluesky_reply_target";

    public override string Description => "Set the active draft's Bluesky reply target from a strict URL in the form https://bsky.app/profile/<handle>/post/<rkey>. Once set, the reply target cannot be changed.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "url": {
              "type": "string",
              "description": "A Bluesky post URL in the exact format https://bsky.app/profile/<handle>/post/<rkey>."
            }
          },
          "required": ["url"]
        }
        """).RootElement.Clone();

    public override async Task<SetBlueskyReplyTargetToolResult> ExecuteAsync(SetBlueskyReplyTargetArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (!draftId.HasValue)
        {
            return new SetBlueskyReplyTargetToolResult(false, "No active draft context.", Error: "No active draft context.");
        }

        using var scope = _scopeFactory.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<IBlueskyReplyTargetResolver>();
        var draftsService = scope.ServiceProvider.GetRequiredService<DraftsService>();

        var resolution = await resolver.ResolveAsync(args.Url, ct);
        if (!resolution.Success)
        {
            return new SetBlueskyReplyTargetToolResult(false, "Could not set Bluesky reply target.", Error: resolution.Error ?? "Failed to resolve Bluesky post.");
        }

        try
        {
            var updatedDraft = await draftsService.SetDraftBlueskyReplyTargetAsync(
                userId,
                draftId.Value,
                resolution.ReplyRootUri,
                resolution.ReplyRootCid,
                resolution.ReplyParentUri,
                resolution.ReplyParentCid,
                resolution.ReplyParentUrl,
                resolution.ReplyParentAuthor,
                resolution.ReplyParentText,
                resolution.ReplyParentAvatarUrl,
                ct);

            return new SetBlueskyReplyTargetToolResult(
                true,
                "Bluesky reply target set for this draft.",
                ReplyParentUrl: updatedDraft.BlueskyReplyTarget?.ReplyParentUrl,
                ReplyParentAuthor: updatedDraft.BlueskyReplyTarget?.ReplyParentAuthor,
                ReplyParentText: updatedDraft.BlueskyReplyTarget?.ReplyParentText,
                ReplyParentAvatarUrl: updatedDraft.BlueskyReplyTarget?.ReplyParentAvatarUrl);
        }
        catch (InvalidOperationException ex)
        {
            return new SetBlueskyReplyTargetToolResult(false, ex.Message, Error: ex.Message);
        }
        catch (KeyNotFoundException)
        {
            return new SetBlueskyReplyTargetToolResult(false, "Draft not found or access denied.", Error: "Draft not found or access denied.");
        }
        catch (ArgumentException ex)
        {
            return new SetBlueskyReplyTargetToolResult(false, ex.Message, Error: ex.Message);
        }
    }
}
