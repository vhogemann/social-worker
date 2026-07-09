using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing;

namespace SocialWorker.Api.Features.Chat.Tools;

public record PublishPlatformArgs(string Platform);

public class PublishPlatformTool : ChatToolBase<PublishPlatformArgs, object>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public PublishPlatformTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override string Name => "publish";
    public override string Description => "Triggers the publication of a drafted thread to a target platform. This is only allowed when the draft's platform variant is in the 'Ready' stage.";
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

    public override async Task<object> ExecuteAsync(PublishPlatformArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (draftId == null)
            return new { error = "No active draft context." };

        string platform = args.Platform ?? "";

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publishers = scope.ServiceProvider.GetServices<IPublisher>();

        var thread = await db.PlatformThreads
            .FirstOrDefaultAsync(t => t.DraftId == draftId && string.Equals(t.Platform, platform, StringComparison.OrdinalIgnoreCase), ct);

        if (thread == null)
        {
            return new { error = $"No platform thread found for platform '{platform}' in this draft." };
        }

        if (thread.Stage != PlatformThreadStage.Ready)
        {
            return new { error = $"Cannot publish. The thread for '{platform}' is currently in stage '{thread.Stage}', but must be 'Ready'." };
        }

        var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId && a.Platform == platform, ct);
        if (account == null)
        {
            return new { error = $"No connected account found for platform: {platform}" };
        }

        var publisher = publishers.FirstOrDefault(p => string.Equals(p.Platform, platform, StringComparison.OrdinalIgnoreCase));
        if (publisher == null)
        {
            return new { error = $"No publisher configured for platform: {platform}" };
        }

        var result = await publisher.PublishAsync(thread, account, ct);

        if (result.Success)
        {
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
            
            return new
            {
                success = true,
                message = $"Successfully published {result.Posts.Count} segments to {platform}.",
                posts = result.Posts
            };
        }
        else
        {
            return new
            {
                success = false,
                error = result.ErrorMessage,
                authUrl = result.AuthUrl
            };
        }
    }
}
