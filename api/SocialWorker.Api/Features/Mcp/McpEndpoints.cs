using System;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SocialWorker.Api.Features.Mcp.Models;
using SocialWorker.Api.Features.Mcp.Services;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Mcp;

public static class McpEndpoints
{
    public static void MapMcpEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/mcp").RequireAuthorization();

        // 1. SSE Connection Endpoint (Standard MCP SSE Transport)
        group.MapGet("/sse", async (
            HttpContext ctx,
            ClaimsPrincipal principal,
            McpSessionManager sessionManager,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null)
            {
                ctx.Response.StatusCode = 401;
                return;
            }

            ctx.Response.ContentType = "text/event-stream";
            ctx.Response.Headers["Cache-Control"] = "no-cache, no-transform";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";
            await ctx.Response.StartAsync(ct);

            var session = sessionManager.CreateSession(userId.Value);
            var endpointMessage = $"event: endpoint\ndata: /api/mcp/message?sessionId={session.SessionId}\n\n";
            await ctx.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(endpointMessage), ct);
            await ctx.Response.Body.FlushAsync(ct);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var response = await session.OutgoingChannel.Reader.ReadAsync(ct);
                    var json = JsonSerializer.Serialize(response);
                    var msg = $"event: message\ndata: {json}\n\n";
                    await ctx.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(msg), ct);
                    await ctx.Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                sessionManager.RemoveSession(session.SessionId);
            }
        });

        // 2. Client Message Endpoint (Standard MCP SSE Transport)
        group.MapPost("/message", async (
            HttpContext ctx,
            ClaimsPrincipal principal,
            McpSessionManager sessionManager,
            McpServer mcpServer,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (!ctx.Request.Query.TryGetValue("sessionId", out var sessionIdVal) || string.IsNullOrWhiteSpace(sessionIdVal))
            {
                return Results.BadRequest("Missing 'sessionId' query parameter.");
            }

            var session = sessionManager.GetSession(sessionIdVal.ToString());
            if (session == null)
            {
                return Results.NotFound("Session not found or expired.");
            }

            var request = await ctx.Request.ReadFromJsonAsync<McpRequest>(cancellationToken: ct);
            if (request == null)
            {
                return Results.BadRequest("Invalid JSON-RPC request.");
            }

            var response = await mcpServer.HandleRequestAsync(request, session.UserId, ct);
            if (response != null)
            {
                await session.OutgoingChannel.Writer.WriteAsync(response, ct);
            }

            return Results.Accepted();
        });

        // 3. Direct Stateless JSON-RPC Endpoint (Convenience for CLI/cURL)
        group.MapPost("/", async (
            ClaimsPrincipal principal,
            McpServer mcpServer,
            McpRequest request,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var response = await mcpServer.HandleRequestAsync(request, userId.Value, ct);
            return response != null ? Results.Ok(response) : Results.NoContent();
        });
    }
}
