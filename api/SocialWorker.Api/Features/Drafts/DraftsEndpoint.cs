using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Drafts;

public static class DraftsEndpoint
{
    public static void MapDraftEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/drafts").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, DraftsService draftsService, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var drafts = await draftsService.GetDraftsForUserAsync(userId.Value, ct);
            return Results.Ok(drafts);
        });

        group.MapPost("/", async (ClaimsPrincipal principal, DraftsService draftsService, CreateDraftRequest req, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await draftsService.CreateDraftAsync(userId.Value, req.Title, req.Content, req.TargetPlatform, ct);
            return Results.Created($"/api/drafts/{result.Id}", result);
        });

        group.MapGet("/{id:guid}", async (ClaimsPrincipal principal, DraftsService draftsService, Guid id, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            try
            {
                var draft = await draftsService.GetDraftByIdAsync(userId.Value, id, ct);
                return Results.Ok(draft);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapPatch("/{id:guid}", async (ClaimsPrincipal principal, DraftsService draftsService, Guid id, UpdateDraftRequest req, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            try
            {
                var result = await draftsService.UpdateDraftAsync(userId.Value, id, req.Title, req.Content, req.Status, req.ChatHistory, req.ChatSummary, req.LastSummarizedMessageCount, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapGet("/{id:guid}/threads", async (ClaimsPrincipal principal, DraftsService draftsService, Guid id, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            try
            {
                var threads = await draftsService.GetPlatformThreadsForDraftAsync(userId.Value, id, ct);
                return Results.Ok(threads);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapPost("/{id:guid}/threads", async (ClaimsPrincipal principal, DraftsService draftsService, Guid id, CreatePlatformThreadRequest req, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(req.Platform))
            {
                return Results.BadRequest("Platform is required");
            }

            try
            {
                var result = await draftsService.CreatePlatformThreadAsync(userId.Value, id, req.Platform, ct);
                return Results.Created($"/api/drafts/{id}/threads/{result.Id}", result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(ex.Message);
            }
        });

        group.MapPatch("/{id:guid}/threads/{threadId:guid}", async (
            ClaimsPrincipal principal,
            DraftsService draftsService,
            Guid id,
            Guid threadId,
            UpdatePlatformThreadRequest req,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            try
            {
                var result = await draftsService.UpdatePlatformThreadAsync(userId.Value, id, threadId, req.Content, req.Stage, ct);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });
    }

    public static List<string> SplitMarkdownIntoSegments(string markdown) => DraftsService.SplitMarkdownIntoSegments(markdown);
    public static DraftsService.SegmentMediaAnalysis AnalyzeSegmentMedia(string segmentContent) => DraftsService.AnalyzeSegmentMedia(segmentContent);
}

public sealed record CreateDraftRequest(string? Title, string? Content, string? TargetPlatform = null);

public sealed record UpdateDraftRequest(
    string? Title,
    string? Content,
    string? Status = null,
    string? ChatHistory = null,
    string? ChatSummary = null,
    int? LastSummarizedMessageCount = null);

public sealed record CreatePlatformThreadRequest(string Platform);

public sealed record UpdatePlatformThreadRequest(
    string? Stage = null,
    string? Content = null);