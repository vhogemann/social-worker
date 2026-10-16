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
        var prefixes = new[] { "/api/mcp", "/mcp" };

        foreach (var prefix in prefixes)
        {
            var group = app.MapGroup(prefix).RequireAuthorization();

            async Task HandleSse(
                HttpContext ctx,
                ClaimsPrincipal principal,
                McpSessionManager sessionManager,
                CancellationToken ct)
            {
                var baseUrl = GetBaseUrl(ctx);
                var userId = principal.GetUserId();
                if (userId is null)
                {
                    ctx.Response.StatusCode = 401;
                    ctx.Response.Headers.Append("WWW-Authenticate",
                        $"Bearer realm=\"mcp\", resource_metadata=\"{baseUrl}/.well-known/oauth-protected-resource\"");
                    return;
                }

                ctx.Response.ContentType = "text/event-stream";
                ctx.Response.Headers["Cache-Control"] = "no-cache, no-transform";
                ctx.Response.Headers["X-Accel-Buffering"] = "no";
                await ctx.Response.StartAsync(ct);

                var session = sessionManager.CreateSession(userId.Value);
                var endpointMessage = $"event: endpoint\ndata: {baseUrl}{prefix}/message?sessionId={session.SessionId}\n\n";
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
            }

            group.MapGet("/sse", HandleSse);
            group.MapGet("/", HandleSse);

            // 2. Client Message Endpoint (Standard MCP SSE Transport)
            group.MapPost("/message", async (
                HttpContext ctx,
                McpSessionManager sessionManager,
                McpServer mcpServer,
                CancellationToken ct) =>
            {
                if (!ctx.Request.Query.TryGetValue("sessionId", out var sessionIdVal) || string.IsNullOrWhiteSpace(sessionIdVal))
                {
                    return Results.BadRequest(new ApiErrorResponse("Missing 'sessionId' query parameter."));
                }

                var session = sessionManager.GetSession(sessionIdVal.ToString());
                if (session == null)
                {
                    return Results.NotFound(new ApiErrorResponse("Session not found or expired."));
                }

                var request = await ctx.Request.ReadFromJsonAsync<McpRequest>(cancellationToken: ct);
                if (request == null)
                {
                    return Results.BadRequest(new ApiErrorResponse("Invalid JSON-RPC request."));
                }

                var response = await mcpServer.HandleRequestAsync(request, session.UserId, ct);
                if (response != null)
                {
                    await session.OutgoingChannel.Writer.WriteAsync(response, ct);
                }

                return Results.Accepted();
            }).AllowAnonymous();

            // 3. Fallback POST on /sse (in case client POSTs to SSE URL)
            group.MapPost("/sse", async (
                HttpContext ctx,
                ClaimsPrincipal principal,
                McpSessionManager sessionManager,
                McpServer mcpServer,
                CancellationToken ct) =>
            {
                if (ctx.Request.Query.TryGetValue("sessionId", out var sessionIdVal) && !string.IsNullOrWhiteSpace(sessionIdVal))
                {
                    var session = sessionManager.GetSession(sessionIdVal.ToString());
                    if (session == null)
                    {
                        return Results.NotFound(new ApiErrorResponse("Session not found or expired."));
                    }

                    var request = await ctx.Request.ReadFromJsonAsync<McpRequest>(cancellationToken: ct);
                    if (request == null) return Results.BadRequest(new ApiErrorResponse("Invalid JSON-RPC request."));

                    var response = await mcpServer.HandleRequestAsync(request, session.UserId, ct);
                    if (response != null)
                    {
                        await session.OutgoingChannel.Writer.WriteAsync(response, ct);
                    }

                    return Results.Accepted();
                }

                var userId = principal.GetUserId();
                if (userId is null)
                {
                    var baseUrl = GetBaseUrl(ctx);
                    ctx.Response.Headers.Append("WWW-Authenticate",
                        $"Bearer realm=\"mcp\", resource_metadata=\"{baseUrl}/.well-known/oauth-protected-resource\"");
                    return Results.Unauthorized();
                }

                var directReq = await ctx.Request.ReadFromJsonAsync<McpRequest>(cancellationToken: ct);
                if (directReq == null) return Results.BadRequest(new ApiErrorResponse("Invalid JSON-RPC request."));

                var directRes = await mcpServer.HandleRequestAsync(directReq, userId.Value, ct);
                return directRes != null ? Results.Ok(directRes) : Results.NoContent();
            }).AllowAnonymous();

            // 4. Direct Stateless JSON-RPC Endpoint (Streamable HTTP / CLI / cURL)
            async Task<IResult> HandleDirectJsonRpc(
                HttpContext ctx,
                ClaimsPrincipal principal,
                McpServer mcpServer,
                McpRequest request,
                CancellationToken ct)
            {
                var userId = principal.GetUserId();
                if (userId is null)
                {
                    var baseUrl = GetBaseUrl(ctx);
                    ctx.Response.Headers.Append("WWW-Authenticate",
                        $"Bearer realm=\"mcp\", resource_metadata=\"{baseUrl}/.well-known/oauth-protected-resource\"");
                    return Results.Unauthorized();
                }

                var response = await mcpServer.HandleRequestAsync(request, userId.Value, ct);
                return response != null ? Results.Ok(response) : Results.NoContent();
            }

            group.MapPost("/", HandleDirectJsonRpc);
        }
    }

    public static string GetBaseUrl(HttpContext ctx)
    {
        var scheme = ctx.Request.Headers["X-Forwarded-Proto"].ToString();
        if (string.IsNullOrWhiteSpace(scheme))
        {
            scheme = ctx.Request.Scheme;
        }

        var host = ctx.Request.Headers["X-Forwarded-Host"].ToString();
        if (string.IsNullOrWhiteSpace(host))
        {
            host = ctx.Request.Host.Value;
        }

        return $"{scheme}://{host}{ctx.Request.PathBase}";
    }
}
