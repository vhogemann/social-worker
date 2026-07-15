using System.Security.Claims;
using System.Text;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Chat;

public static class ChatEndpoint
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        app.MapPost("/api/chat", async (HttpContext ctx, ChatService svc, ILogger<ChatService> log, CancellationToken ct) =>
        {
            var userId = ctx.User.GetUserId();
            if (userId is null)
            {
                ctx.Response.StatusCode = 401;
                return;
            }

            ChatModels.ChatRequest? req;
            try
            {
                req = await ctx.Request.ReadFromJsonAsync<ChatModels.ChatRequest>(ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to parse chat request JSON payload.");
                ctx.Response.StatusCode = 400;
                return;
            }

            if (req is null || req.Messages.Count == 0)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("messages required", ct);
                return;
            }

            ctx.Response.ContentType = "text/plain; charset=utf-8";
            ctx.Response.Headers["x-vercel-ai-data-stream"] = "v1";
            ctx.Response.Headers["Cache-Control"] = "no-cache, no-transform";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";
            ctx.Response.StatusCode = 200;
            await ctx.Response.StartAsync(ct);

            await using var writer = ctx.Response.Body;
            await foreach (var line in svc.StreamAsync(req, userId.Value, ct))
            {
                var bytes = Encoding.UTF8.GetBytes(line);
                await writer.WriteAsync(bytes, ct);
                await writer.FlushAsync(ct);
            }
        }).RequireAuthorization();
    }
}