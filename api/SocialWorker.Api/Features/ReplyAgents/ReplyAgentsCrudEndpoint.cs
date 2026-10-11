using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.ReplyAgents;

public static class ReplyAgentsCrudEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reply-agents")
            .RequireAuthorization();

        group.MapGet("", async (
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var agents = await db.ReplyAgents
                .Include(a => a.Workflows)
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var result = agents.Select(a => new ReplyAgentDto(a)).ToList();
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var agent = await db.ReplyAgents
                .Include(a => a.Workflows)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (agent == null) return Results.NotFound("Reply Agent not found.");

            return Results.Ok(new ReplyAgentDto(agent));
        });

        group.MapPost("", async (
            CreateReplyAgentRequest req,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            if (userId == null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest("Agent name is required.");
            }

            var agent = ReplyAgent.Create(
                userId.Value,
                req.Name,
                req.PersonaPrompt,
                req.Description,
                req.MaxCommentAgeHours,
                req.MaxPostAgeDays,
                req.MinRankingScore,
                req.MaxRepliesPerCommentThread,
                req.MaxRepliesPerUser,
                req.MaxThreadDepth,
                req.AutoPublishReplies,
                req.CheckIntervalMinutes);
            agent.IsEnabled = req.IsEnabled;

            db.ReplyAgents.Add(agent);
            await db.SaveChangesAsync();

            return Results.Created($"/api/reply-agents/{agent.Id}", new ReplyAgentDto(agent));
        });

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateReplyAgentRequest req,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var agent = await db.ReplyAgents
                .Include(a => a.Workflows)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (agent == null) return Results.NotFound("Reply Agent not found.");

            if (string.IsNullOrWhiteSpace(req.Name))
            {
                return Results.BadRequest("Agent name is required.");
            }

            agent.Name = req.Name.Trim();
            agent.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
            agent.IsEnabled = req.IsEnabled;
            agent.PersonaPrompt = string.IsNullOrWhiteSpace(req.PersonaPrompt)
                ? "You are a helpful and polite social media assistant."
                : req.PersonaPrompt.Trim();
            agent.MaxCommentAgeHours = Math.Max(1, req.MaxCommentAgeHours);
            agent.MaxPostAgeDays = Math.Max(1, req.MaxPostAgeDays);
            agent.MinRankingScore = Math.Clamp(req.MinRankingScore, 1, 10);
            agent.MaxRepliesPerCommentThread = Math.Max(1, req.MaxRepliesPerCommentThread);
            agent.MaxRepliesPerUser = Math.Max(1, req.MaxRepliesPerUser);
            agent.MaxThreadDepth = Math.Max(1, req.MaxThreadDepth);
            agent.AutoPublishReplies = req.AutoPublishReplies;
            agent.CheckIntervalMinutes = Math.Max(1, req.CheckIntervalMinutes);
            agent.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return Results.Ok(new ReplyAgentDto(agent));
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            AppDbContext db) =>
        {
            var userId = user.GetUserId();
            var agent = await db.ReplyAgents
                .Include(a => a.Workflows)
                .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (agent == null) return Results.NotFound("Reply Agent not found.");

            foreach (var wf in agent.Workflows)
            {
                wf.ReplyAgentId = null;
            }

            db.ReplyAgents.Remove(agent);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
