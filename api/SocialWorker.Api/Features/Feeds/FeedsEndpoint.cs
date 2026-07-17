using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure;

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
        });

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
                .Select(s => new FeedSubscriptionDto(
                    s.Id,
                    s.Title,
                    s.FeedUrl,
                    s.WebsiteUrl,
                    s.InstructionPrompt,
                    s.AutoPublish,
                    s.LastPolledAt,
                    s.IncludeFilters,
                    s.ExcludeFilters,
                    s.CreatedAt))
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

            if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.FeedUrl))
            {
                return Results.BadRequest("Title and FeedUrl are required.");
            }

            var subscription = new FeedSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Title = req.Title.Trim(),
                FeedUrl = req.FeedUrl.Trim(),
                WebsiteUrl = req.WebsiteUrl?.Trim(),
                InstructionPrompt = req.InstructionPrompt?.Trim() ?? "Summarize this article as a thread.",
                AutoPublish = req.AutoPublish,
                IncludeFilters = req.IncludeFilters?.Trim(),
                ExcludeFilters = req.ExcludeFilters?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            db.FeedSubscriptions.Add(subscription);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/feeds/{subscription.Id}", new FeedSubscriptionDto(
                subscription.Id,
                subscription.Title,
                subscription.FeedUrl,
                subscription.WebsiteUrl,
                subscription.InstructionPrompt,
                subscription.AutoPublish,
                subscription.LastPolledAt,
                subscription.IncludeFilters,
                subscription.ExcludeFilters,
                subscription.CreatedAt));
        });

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

            if (subscription == null) return Results.NotFound();

            if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.FeedUrl))
            {
                return Results.BadRequest("Title and FeedUrl are required.");
            }

            subscription.Title = req.Title.Trim();
            subscription.FeedUrl = req.FeedUrl.Trim();
            subscription.WebsiteUrl = req.WebsiteUrl?.Trim();
            subscription.InstructionPrompt = req.InstructionPrompt?.Trim() ?? "Summarize this article as a thread.";
            subscription.AutoPublish = req.AutoPublish;
            subscription.IncludeFilters = req.IncludeFilters?.Trim();
            subscription.ExcludeFilters = req.ExcludeFilters?.Trim();

            await db.SaveChangesAsync(ct);

            return Results.Ok(new FeedSubscriptionDto(
                subscription.Id,
                subscription.Title,
                subscription.FeedUrl,
                subscription.WebsiteUrl,
                subscription.InstructionPrompt,
                subscription.AutoPublish,
                subscription.LastPolledAt,
                subscription.IncludeFilters,
                subscription.ExcludeFilters,
                subscription.CreatedAt));
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

            if (subscription == null) return Results.NotFound();

            db.FeedSubscriptions.Remove(subscription);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        });

        group.MapPost("/{id:guid}/trigger", async (
            ClaimsPrincipal principal,
            FeedPollingHostedService pollingService,
            Guid id,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            // Run polling synchronously for this single subscription so that the user gets immediate response
            await pollingService.PollSubscriptionAsync(id, ct);
            return Results.Ok(new { Success = true, Message = "Polling triggered successfully." });
        });

        group.MapGet("/queue", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var items = await db.FeedIngestionQueueItems
                .Include(q => q.FeedSubscription)
                .Where(q => q.FeedSubscription.UserId == userId.Value)
                .OrderByDescending(q => q.CreatedAt)
                .Take(200)
                .Select(q => new FeedQueueItemDto(
                    q.Id,
                    q.FeedSubscriptionId,
                    q.FeedSubscription.Title,
                    q.ItemTitle,
                    q.ItemLink,
                    q.Status.ToString(),
                    q.AttemptCount,
                    q.MaxAttempts,
                    q.NextAttemptAt,
                    q.LastError,
                    q.CreatedAt,
                    q.UpdatedAt,
                    q.CompletedAt))
                .ToListAsync(ct);

            return Results.Ok(items);
        });

        group.MapPost("/queue/{queueItemId:guid}/retry", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            Guid queueItemId,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var item = await db.FeedIngestionQueueItems
                .Include(q => q.FeedSubscription)
                .FirstOrDefaultAsync(q => q.Id == queueItemId && q.FeedSubscription.UserId == userId.Value, ct);

            if (item == null) return Results.NotFound();
            if (item.Status == FeedQueueItemStatus.Processing)
            {
                return Results.BadRequest("Cannot retry an item that is currently processing.");
            }

            item.Status = FeedQueueItemStatus.Pending;
            item.AttemptCount = 0;
            item.LastError = null;
            item.NextAttemptAt = DateTime.UtcNow;
            item.CompletedAt = null;
            item.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { Success = true });
        });

        group.MapDelete("/queue/{queueItemId:guid}", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            Guid queueItemId,
            CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            if (userId is null) return Results.Unauthorized();

            var item = await db.FeedIngestionQueueItems
                .Include(q => q.FeedSubscription)
                .FirstOrDefaultAsync(q => q.Id == queueItemId && q.FeedSubscription.UserId == userId.Value, ct);

            if (item == null) return Results.NotFound();
            db.FeedIngestionQueueItems.Remove(item);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}

public sealed record DiscoverFeedRequest(string Url);

public sealed record CreateFeedSubscriptionRequest(
    string Title,
    string FeedUrl,
    string? WebsiteUrl,
    string InstructionPrompt,
    bool AutoPublish,
    string? IncludeFilters,
    string? ExcludeFilters);

public sealed record FeedSubscriptionDto(
    Guid Id,
    string Title,
    string FeedUrl,
    string? WebsiteUrl,
    string InstructionPrompt,
    bool AutoPublish,
    DateTime? LastPolledAt,
    string? IncludeFilters,
    string? ExcludeFilters,
    DateTime CreatedAt);

public sealed record FeedQueueItemDto(
    Guid Id,
    Guid FeedSubscriptionId,
    string FeedSubscriptionTitle,
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
