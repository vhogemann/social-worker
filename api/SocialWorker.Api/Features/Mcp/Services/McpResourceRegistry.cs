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

using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Features.Workflows;

namespace SocialWorker.Api.Features.Mcp.Services;

public sealed class McpResourceRegistry
{
    private readonly AppDbContext _db;
    private readonly DraftsService _draftsService;
    private readonly SocialWorker.Api.Features.Media.MediaService? _mediaService;

    public McpResourceRegistry(AppDbContext db, DraftsService draftsService, SocialWorker.Api.Features.Media.MediaService? mediaService = null)
    {
        _db = db;
        _draftsService = draftsService;
        _mediaService = mediaService;
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

        var replyAgentWorkflows = await _db.AutomationWorkflows
            .AsNoTracking()
            .Where(w => w.UserId == userId && w.ReplyAgent != null)
            .Select(w => new { w.Id, w.Title })
            .ToListAsync(ct);

        foreach (var wf in replyAgentWorkflows)
        {
            resources.Add(new(
                $"socialworker://workflows/{wf.Id}/reply-agent",
                $"Reply Agent: {wf.Title}",
                $"Autonomous reply agent configuration and stats for workflow '{wf.Title}'",
                "application/json"));
        }

        var recentMedia = await _db.MediaAssets
            .AsNoTracking()
            .Where(m => m.Draft != null && m.Draft.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(15)
            .Select(m => new { m.Id, m.FileName, m.MimeType, m.Width, m.Height })
            .ToListAsync(ct);

        foreach (var media in recentMedia)
        {
            resources.Add(new(
                $"socialworker://media/{media.Id}",
                $"Image: {media.FileName}",
                $"Attached image {media.FileName} ({media.Width}x{media.Height})",
                media.MimeType));
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
                .Include(w => w.ReplyAgent)
                .ToListAsync(ct);

            var dtos = workflows.Select(w => new AutomationWorkflowDto(w)).ToList();
            return new McpResourceReadResult(new List<McpResourceContent>
            {
                new(uri, "application/json", JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true }))
            });
        }

        if (uri.StartsWith("socialworker://workflows/", StringComparison.OrdinalIgnoreCase) && uri.EndsWith("/reply-agent", StringComparison.OrdinalIgnoreCase))
        {
            var middle = uri.Substring("socialworker://workflows/".Length);
            var idStr = middle.Substring(0, middle.Length - "/reply-agent".Length).Trim();
            if (Guid.TryParse(idStr, out var workflowId))
            {
                var workflow = await _db.AutomationWorkflows
                    .AsNoTracking()
                    .Include(w => w.ReplyAgent)
                    .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

                if (workflow?.ReplyAgent != null)
                {
                    var assignedWorkflows = await _db.AutomationWorkflows
                        .AsNoTracking()
                        .Where(w => w.ReplyAgentId == workflow.ReplyAgent.Id)
                        .Select(w => new ReplyAgentWorkflowSummaryDto(w.Id, w.Title))
                        .ToListAsync(ct);

                    var dto = new ReplyAgentDto(workflow.ReplyAgent, assignedWorkflows);
                    return new McpResourceReadResult(new List<McpResourceContent>
                    {
                        new(uri, "application/json", JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }))
                    });
                }
            }
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

        if (uri.StartsWith("socialworker://media/", StringComparison.OrdinalIgnoreCase))
        {
            var idStr = uri.Substring("socialworker://media/".Length).Trim();
            if (Guid.TryParse(idStr, out var mediaId))
            {
                var asset = await _db.MediaAssets
                    .AsNoTracking()
                    .Include(m => m.Draft)
                    .FirstOrDefaultAsync(m => m.Id == mediaId, ct);

                if (asset != null && (asset.Draft == null || asset.Draft.UserId == userId) && _mediaService != null)
                {
                    try
                    {
                        var (fullPath, mimeType) = await _mediaService.GetMediaFileAsync(mediaId, ct);
                        if (System.IO.File.Exists(fullPath))
                        {
                            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath, ct);
                            var base64 = Convert.ToBase64String(bytes);
                            return new McpResourceReadResult(new List<McpResourceContent>
                            {
                                McpResourceContent.AsBlob(uri, mimeType, base64)
                            });
                        }
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        }

        return null;
    }
}
