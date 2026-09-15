using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Auth;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.Feeds.Handlers;
using SocialWorker.Api.Infrastructure;
using SocialWorker.Api.Infrastructure.Validation;

namespace SocialWorker.Api.Features.Feeds;

public static class FeedsEndpoint
{
    public static void MapFeedsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/feeds").RequireAuthorization();

        group.MapPost("/discover", async (
            ClaimsPrincipal principal,
            FeedDiscoveryService discoveryService,
            DiscoverFeedRequest req,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var result = await discoveryService.DiscoverAsync(req.Url);
            return Results.Ok(result);
        }).WithValidation<DiscoverFeedRequest>();

        group.MapGet("/", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var subscriptions = await db.FeedSubscriptions
                .Where(s => s.UserId == userId.Value)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new FeedSubscriptionDto(s))
                .ToListAsync(ct);

            return Results.Ok(subscriptions);
        });

        group.MapPost("/", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            CreateFeedSubscriptionRequest req,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var subscription = FeedSubscription.Create(userId.Value, req);

            db.FeedSubscriptions.Add(subscription);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/feeds/{subscription.Id}", new FeedSubscriptionDto(subscription));
        }).WithValidation<CreateFeedSubscriptionRequest>();

        group.MapPut("/{id:guid}", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            Guid id,
            CreateFeedSubscriptionRequest req,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var subscription = await db.FeedSubscriptions
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value, ct);

            if (subscription == null) return Results.NotFound(new ApiErrorResponse("Feed subscription not found."));

            if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.FeedUrl))
            {
                return Results.BadRequest(new ApiErrorResponse("Title and FeedUrl are required."));
            }

            subscription.Title = req.Title.Trim();
            subscription.FeedUrl = req.FeedUrl.Trim();
            subscription.WebsiteUrl = req.WebsiteUrl?.Trim();
            subscription.IncludeFilters = req.IncludeFilters?.Trim();
            subscription.ExcludeFilters = req.ExcludeFilters?.Trim();

            await db.SaveChangesAsync(ct);

            return Results.Ok(new FeedSubscriptionDto(subscription));
        });

        group.MapDelete("/{id:guid}", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            Guid id,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var subscription = await db.FeedSubscriptions
                .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId.Value, ct);

            if (subscription == null) return Results.NotFound(new ApiErrorResponse("Feed subscription not found."));

            db.FeedSubscriptions.Remove(subscription);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/trigger", async (
            ClaimsPrincipal principal,
            FeedPollingService pollingService,
            Guid id,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            await pollingService.PollSubscriptionAsync(id, ct);
            return Results.Ok(new ApiSuccessResponse(true, "Polling triggered successfully."));
        });

        group.MapGet("/queue", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            Guid? workflowId,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var userSubscriptions = await db.FeedSubscriptions
                .Where(s => s.UserId == userId.Value)
                .ToDictionaryAsync(s => s.Id, s => s.Title, ct);

            var userWorkflows = await db.AutomationWorkflows
                .Where(w => w.UserId == userId.Value)
                .ToDictionaryAsync(w => w.Id, w => w.Title, ct);

            var tasks = await db.BackgroundTasks
                .Where(t => t.TaskType == "feed-ingestion")
                .OrderByDescending(t => t.CreatedAt)
                .Take(200)
                .ToListAsync(ct);

            var items = new List<FeedQueueItemDto>();
            foreach (var t in tasks)
            {
                FeedIngestionPayload? payload = null;
                try
                {
                    payload = JsonSerializer.Deserialize<FeedIngestionPayload>(t.PayloadJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch
                {
                    // Ignore corrupted payload
                }

                if (payload == null) continue;
                if (!userSubscriptions.TryGetValue(payload.FeedSubscriptionId, out var subTitle)) continue;
                if (workflowId.HasValue && payload.AutomationWorkflowId != workflowId.Value) continue;

                userWorkflows.TryGetValue(payload.AutomationWorkflowId, out var wfTitle);

                items.Add(new FeedQueueItemDto(
                    t.Id,
                    payload.FeedSubscriptionId,
                    subTitle,
                    payload.AutomationWorkflowId,
                    wfTitle ?? "",
                    payload.ItemTitle,
                    payload.ItemLink,
                    t.Status.ToString(),
                    t.AttemptCount,
                    t.MaxAttempts,
                    t.NextAttemptAt,
                    t.LastError,
                    t.CreatedAt,
                    t.UpdatedAt,
                    t.CompletedAt));
            }

            return Results.Ok(items);
        });

        group.MapPost("/queue/{queueItemId:guid}/retry", async (
            ClaimsPrincipal principal,
            IBackgroundTaskQueue queue,
            Guid queueItemId,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var success = await queue.RetryTaskAsync(queueItemId, ct);
            if (!success) return Results.NotFound(new ApiErrorResponse("Feed queue item not found."));

            return Results.Ok(new ApiSuccessResponse(true));
        });

        group.MapDelete("/queue/{queueItemId:guid}", async (
            ClaimsPrincipal principal,
            IBackgroundTaskQueue queue,
            Guid queueItemId,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var success = await queue.DeleteTaskAsync(queueItemId, ct);
            if (!success) return Results.NotFound(new ApiErrorResponse("Feed queue item not found."));

            return Results.NoContent();
        });
    }
}

public sealed record DiscoverFeedRequest(string Url);

public sealed record CreateFeedSubscriptionRequest(
    string Title,
    string FeedUrl,
    string? WebsiteUrl,
    string? IncludeFilters,
    string? ExcludeFilters);

public sealed record FeedSubscriptionDto(
    Guid Id,
    string Title,
    string FeedUrl,
    string? WebsiteUrl,
    DateTime? LastPolledAt,
    string? IncludeFilters,
    string? ExcludeFilters,
    DateTime CreatedAt)
{
    public FeedSubscriptionDto(Data.Entities.FeedSubscription s)
        : this(s.Id, s.Title, s.FeedUrl, s.WebsiteUrl, s.LastPolledAt, s.IncludeFilters, s.ExcludeFilters, s.CreatedAt)
    {
    }
};

public sealed record FeedQueueItemDto(
    Guid Id,
    Guid FeedSubscriptionId,
    string FeedSubscriptionTitle,
    Guid AutomationWorkflowId,
    string AutomationWorkflowTitle,
    string ItemTitle,
    string ItemLink,
    string Status,
    int AttemptCount,
    int MaxAttempts,
    DateTime NextAttemptAt,
    string? LastError,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? CompletedAt);
