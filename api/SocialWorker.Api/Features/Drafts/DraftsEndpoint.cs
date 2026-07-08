using System;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using System.Text.RegularExpressions;

namespace SocialWorker.Api.Features.Drafts;

public static class DraftsEndpoint
{
    public static void MapDraftEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/drafts").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var drafts = await db.Drafts
                .Where(d => d.UserId == userId.Value && d.Status != DraftStatus.Deleted)
                .OrderByDescending(d => d.UpdatedAt)
                .Select(d => new
                {
                    d.Id,
                    d.Title,
                    Status = d.Status.ToString(),
                    d.Content,
                    Threads = d.Threads.Select(t => new { t.Id, t.Platform, Stage = t.Stage.ToString() }).ToList(),
                    d.CreatedAt,
                    d.UpdatedAt
                })
                .ToListAsync();
            return Results.Ok(drafts);
        });

        group.MapPost("/", async (ClaimsPrincipal principal, AppDbContext db, IServiceScopeFactory scopeFactory, CreateDraftRequest req) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var draft = new Draft
            {
                Title = string.IsNullOrWhiteSpace(req.Title) ? "Untitled" : req.Title,
                Content = req.Content,
                UserId = userId.Value
            };
            db.Drafts.Add(draft);
            await db.SaveChangesAsync();

            var thread = new PlatformThread
            {
                DraftId = draft.Id,
                Platform = "Bluesky",
                Stage = PlatformThreadStage.Draft,
                Content = req.Content
            };
            db.PlatformThreads.Add(thread);
            await db.SaveChangesAsync();

            await ReconcileSegmentsAsync(db, draft, req.Content ?? "");
            await db.SaveChangesAsync();

            await SocialWorker.Api.Features.Sources.SourcesEndpoint.ReconcileSourcesAsync(db, scopeFactory, draft, req.Content ?? "");
            await db.SaveChangesAsync();

            return Results.Created($"/api/drafts/{draft.Id}", new
            {
                draft.Id,
                draft.Title,
                Status = draft.Status.ToString(),
                draft.Content,
                Threads = new[] { new { thread.Id, thread.Platform, Stage = thread.Stage.ToString(), thread.Content } },
                draft.CreatedAt,
                draft.UpdatedAt
            });
        });

        group.MapGet("/{id:guid}", async (ClaimsPrincipal principal, AppDbContext db, Guid id) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var draft = await db.Drafts
                .Include(d => d.Threads)
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId.Value && d.Status != DraftStatus.Deleted);
            if (draft is null) return Results.NotFound();
            return Results.Ok(new
            {
                draft.Id,
                draft.Title,
                Status = draft.Status.ToString(),
                draft.Content,
                Threads = draft.Threads.Select(t => new { t.Id, t.Platform, Stage = t.Stage.ToString(), t.Content }).ToList(),
                draft.CreatedAt,
                draft.UpdatedAt
            });
        });

        group.MapPatch("/{id:guid}", async (ClaimsPrincipal principal, AppDbContext db, IServiceScopeFactory scopeFactory, Guid id, UpdateDraftRequest req) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var draft = await db.Drafts
                .Include(d => d.Threads)
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId.Value && d.Status != DraftStatus.Deleted);
            if (draft is null) return Results.NotFound();

            if (req.Title is not null)
            {
                draft.Title = string.IsNullOrWhiteSpace(req.Title) ? "Untitled" : req.Title;
            }
            if (req.Content is not null)
            {
                draft.Content = req.Content;
                await ReconcileSegmentsAsync(db, draft, req.Content);
                await SocialWorker.Api.Features.Sources.SourcesEndpoint.ReconcileSourcesAsync(db, scopeFactory, draft, req.Content);
            }
            if (req.Status is not null && Enum.TryParse<DraftStatus>(req.Status, true, out var status))
            {
                if (status == DraftStatus.Deleted)
                {
                    var draftAssets = await db.MediaAssets.Where(m => m.DraftId == draft.Id).ToListAsync();
                    foreach (var asset in draftAssets)
                    {
                        var isShared = await db.MediaAssets.AnyAsync(m => m.Id != asset.Id && m.FilePath == asset.FilePath);
                        if (!isShared)
                        {
                            var fullPath = Path.Combine("/app/uploads", asset.FilePath);
                            if (File.Exists(fullPath))
                            {
                                try { File.Delete(fullPath); } catch {}
                            }
                        }
                    }

                    var draftFolder = Path.Combine("/app/uploads", draft.Id.ToString());
                    if (Directory.Exists(draftFolder) && !Directory.EnumerateFileSystemEntries(draftFolder).Any())
                    {
                        try { Directory.Delete(draftFolder); } catch {}
                    }

                    db.Drafts.Remove(draft);
                }
                else
                {
                    draft.Status = status;
                }
            }

            draft.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                draft.Id,
                draft.Title,
                Status = draft.Status.ToString(),
                draft.Content,
                Threads = draft.Threads.Select(t => new { t.Id, t.Platform, Stage = t.Stage.ToString(), t.Content }).ToList(),
                draft.CreatedAt,
                draft.UpdatedAt
            });
        });

        group.MapGet("/{id:guid}/threads", async (ClaimsPrincipal principal, AppDbContext db, Guid id) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var draftExists = await db.Drafts.AnyAsync(d => d.Id == id && d.UserId == userId.Value && d.Status != DraftStatus.Deleted);
            if (!draftExists) return Results.NotFound();

            var threads = await db.PlatformThreads
                .Where(t => t.DraftId == id)
                .Select(t => new
                {
                    t.Id,
                    t.DraftId,
                    t.Platform,
                    Stage = t.Stage.ToString(),
                    t.Content
                })
                .ToListAsync();
            return Results.Ok(threads);
        });

        group.MapPost("/{id:guid}/threads", async (ClaimsPrincipal principal, AppDbContext db, Guid id, CreatePlatformThreadRequest req) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId.Value && d.Status != DraftStatus.Deleted);
            if (draft is null) return Results.NotFound();

            if (string.IsNullOrWhiteSpace(req.Platform))
            {
                return Results.BadRequest("Platform is required");
            }

            var exists = await db.PlatformThreads.AnyAsync(t => t.DraftId == id && t.Platform == req.Platform);
            if (exists)
            {
                return Results.Conflict($"A thread variant for platform '{req.Platform}' already exists.");
            }

            var thread = new PlatformThread
            {
                DraftId = id,
                Platform = req.Platform,
                Stage = PlatformThreadStage.Draft,
                Content = draft.Content
            };

            db.PlatformThreads.Add(thread);
            await db.SaveChangesAsync();

            return Results.Created($"/api/drafts/{id}/threads/{thread.Id}", new
            {
                thread.Id,
                thread.DraftId,
                thread.Platform,
                Stage = thread.Stage.ToString(),
                thread.Content
            });
        });

        group.MapPatch("/{id:guid}/threads/{threadId:guid}", async (ClaimsPrincipal principal, AppDbContext db, Guid id, Guid threadId, UpdatePlatformThreadRequest req) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var draftExists = await db.Drafts.AnyAsync(d => d.Id == id && d.UserId == userId.Value && d.Status != DraftStatus.Deleted);
            if (!draftExists) return Results.NotFound();

            var thread = await db.PlatformThreads.FirstOrDefaultAsync(t => t.Id == threadId && t.DraftId == id);
            if (thread is null) return Results.NotFound();

            if (req.Content is not null)
            {
                thread.Content = req.Content;
            }

            if (req.Stage is not null && Enum.TryParse<PlatformThreadStage>(req.Stage, true, out var stage))
            {
                if (stage == PlatformThreadStage.Ready || stage == PlatformThreadStage.Sent)
                {
                    if (string.Equals(thread.Platform, "Bluesky", StringComparison.OrdinalIgnoreCase))
                    {
                        var segments = SplitMarkdownIntoSegments(thread.Content ?? "");
                        foreach (var segment in segments)
                        {
                            var analysis = AnalyzeSegmentMedia(segment);
                            if (analysis.HasConflict)
                            {
                                return Results.BadRequest("Bluesky segment contains both images and a YouTube embed. Only images OR one YouTube embed is allowed per post.");
                            }
                        }
                    }
                }
                thread.Stage = stage;
            }

            thread.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                thread.Id,
                thread.DraftId,
                thread.Platform,
                Stage = thread.Stage.ToString(),
                thread.Content
            });
        });
    }

    private static readonly Regex MediaRegex = new(@"!\[.*?\]\(media://([0-9a-fA-F\-]{36})\)", RegexOptions.Compiled);
    private static readonly Regex YouTubeEmbedRegex = new(@"!\[.*?\]\((https?://(?:www\.)?youtube\.com/watch\?v=[\w-]+|https?://youtu\.be/[\w-]+)\)", RegexOptions.Compiled);

    public static SegmentMediaAnalysis AnalyzeSegmentMedia(string segmentContent)
    {
        var imageIds = MediaRegex.Matches(segmentContent)
            .Select(m => Guid.TryParse(m.Groups[1].Value, out var guid) ? guid : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToArray();

        string? youtubeUrl = null;
        var ytMatch = YouTubeEmbedRegex.Match(segmentContent);
        if (ytMatch.Success)
        {
            youtubeUrl = ytMatch.Groups[1].Value;
        }

        bool hasConflict = imageIds.Length > 0 && youtubeUrl != null;

        return new SegmentMediaAnalysis(imageIds, youtubeUrl, hasConflict);
    }

    public record SegmentMediaAnalysis(Guid[] ImageIds, string? YouTubeUrl, bool HasConflict);

    public static async Task ReconcileSegmentsAsync(AppDbContext db, Draft draft, string markdown, CancellationToken ct = default)
    {
        var rawSegments = SplitMarkdownIntoSegments(markdown);
        var existing = await db.ThreadSegments
            .Where(s => s.DraftId == draft.Id)
            .OrderBy(s => s.Position)
            .ToListAsync(ct);

        int max = Math.Max(rawSegments.Count, existing.Count);
        for (int i = 0; i < max; i++)
        {
            if (i < rawSegments.Count)
            {
                var content = rawSegments[i];
                if (i < existing.Count)
                {
                    existing[i].Content = content;
                }
                else
                {
                    db.ThreadSegments.Add(new ThreadSegment
                    {
                        DraftId = draft.Id,
                        Position = i,
                        Content = content
                    });
                }
            }
            else
            {
                db.ThreadSegments.Remove(existing[i]);
            }
        }
    }

    public static List<string> SplitMarkdownIntoSegments(string markdown)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(markdown))
        {
            return new List<string> { "" };
        }

        var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var current = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            if (line.Trim() == "---")
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                if (current.Length > 0)
                {
                    current.AppendLine();
                }
                current.Append(line);
            }
        }
        result.Add(current.ToString().Trim());
        return result;
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var parsed) ? parsed : null;
    }
}

public sealed record CreateDraftRequest(string? Title, string? Content);

public sealed record UpdateDraftRequest(
    string? Title,
    string? Content,
    string? Status = null);

public sealed record CreatePlatformThreadRequest(string Platform);

public sealed record UpdatePlatformThreadRequest(
    string? Stage = null,
    string? Content = null);