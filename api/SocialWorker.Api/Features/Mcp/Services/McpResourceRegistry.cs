using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Mcp.Models;

namespace SocialWorker.Api.Features.Mcp.Services;

public sealed class McpResourceRegistry
{
    private readonly AppDbContext _db;
    private readonly DraftsService _draftsService;

    public McpResourceRegistry(AppDbContext db, DraftsService draftsService)
    {
        _db = db;
        _draftsService = draftsService;
    }

    public async Task<List<McpResourceDefinition>> ListResourcesAsync(Guid userId, CancellationToken ct)
    {
        var resources = new List<McpResourceDefinition>
        {
            new("socialworker://feeds", "Subscribed Feeds", "All RSS and YouTube feed subscriptions", "application/json"),
            new("socialworker://workflows", "Automation Workflows", "All configured automated publishing workflows", "application/json")
        };

        var recentDrafts = await _db.Drafts
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UpdatedAt)
            .Take(10)
            .Select(d => new { d.Id, d.Title })
            .ToListAsync(ct);

        foreach (var draft in recentDrafts)
        {
            resources.Add(new(
                $"socialworker://drafts/{draft.Id}",
                $"Draft: {draft.Title}",
                $"Draft content and attached sources for {draft.Title}",
                "text/markdown"));
        }

        return resources;
    }

    public async Task<McpResourceReadResult?> ReadResourceAsync(string uri, Guid userId, CancellationToken ct)
    {
        if (uri.Equals("socialworker://feeds", StringComparison.OrdinalIgnoreCase))
        {
            var feeds = await _db.FeedSubscriptions
                .AsNoTracking()
                .Where(f => f.UserId == userId)
                .ToListAsync(ct);

            return new McpResourceReadResult(new List<McpResourceContent>
            {
                new(uri, "application/json", JsonSerializer.Serialize(feeds, new JsonSerializerOptions { WriteIndented = true }))
            });
        }

        if (uri.Equals("socialworker://workflows", StringComparison.OrdinalIgnoreCase))
        {
            var workflows = await _db.AutomationWorkflows
                .AsNoTracking()
                .Where(w => w.UserId == userId)
                .Include(w => w.WorkflowSources)
                .Include(w => w.Destinations)
                .ToListAsync(ct);

            return new McpResourceReadResult(new List<McpResourceContent>
            {
                new(uri, "application/json", JsonSerializer.Serialize(workflows, new JsonSerializerOptions { WriteIndented = true }))
            });
        }

        if (uri.StartsWith("socialworker://drafts/", StringComparison.OrdinalIgnoreCase))
        {
            var idStr = uri.Substring("socialworker://drafts/".Length).Trim();
            if (Guid.TryParse(idStr, out var draftId))
            {
                var draft = await _draftsService.GetDraftByIdAsync(userId, draftId, ct);
                var formatted = $"# {draft.Title}\n\nStatus: {draft.Status}\n\n## Content\n\n{draft.Content}\n";
                return new McpResourceReadResult(new List<McpResourceContent>

                {
                    new(uri, "text/markdown", formatted)
                });
            }
        }

        return null;
    }
}
