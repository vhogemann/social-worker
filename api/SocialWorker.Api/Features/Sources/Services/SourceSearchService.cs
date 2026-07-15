using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Features.Sources;

public sealed class SourceSearchService
{
    private readonly AppDbContext _db;

    public SourceSearchService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SourceSearchResultDto> SearchSourcesAsync(Guid userId, string query, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var normalizedQuery = query?.Trim() ?? string.Empty;

        var accessibleSources = _db.Sources
            .Where(s => s.DraftSources.Any(ds => ds.Draft.UserId == userId && ds.Draft.Status != DraftStatus.Deleted));

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            if (_db.Database.IsNpgsql())
            {
                var like = $"%{normalizedQuery}%";
                accessibleSources = accessibleSources.Where(s =>
                    EF.Functions.ILike(s.Reference, like) ||
                    (s.Title != null && EF.Functions.ILike(s.Title, like)) ||
                    (s.Content != null && EF.Functions.ILike(s.Content, like)) ||
                    (s.Summary != null && EF.Functions.ILike(s.Summary, like)));
            }
            else
            {
                var lowered = normalizedQuery.ToLower();
                accessibleSources = accessibleSources.Where(s =>
                    s.Reference.ToLower().Contains(lowered) ||
                    (s.Title != null && s.Title.ToLower().Contains(lowered)) ||
                    (s.Content != null && s.Content.ToLower().Contains(lowered)) ||
                    (s.Summary != null && s.Summary.ToLower().Contains(lowered)));
            }
        }

        var total = await accessibleSources.Select(s => s.Id).Distinct().CountAsync(ct);

        var rows = await accessibleSources
            .OrderByDescending(s => s.AddedAt)
            .Select(s => new
            {
                s.Id,
                s.Kind,
                s.Reference,
                s.Title,
                s.Summary,
                s.TranscriptStatus,
                s.YoutubeVideoId,
                s.AddedAt
            })
            .Distinct()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(s =>
        {
            var links = SourceLinkFields.Build(s.Id, s.Kind, s.Reference, s.Title);
            return new SourceSearchItemDto(
                s.Id,
                s.Kind.ToString(),
                s.Reference,
                s.Title,
                s.Summary,
                s.TranscriptStatus.ToString(),
                s.YoutubeVideoId,
                s.AddedAt,
                links.CanonicalUrl,
                links.CitationLabel,
                links.EmbedKind,
                links.CanonicalEmbedMarkdown,
                links.PlainLinkLine);
        }).ToList();

        return new SourceSearchResultDto(items, total, page, pageSize);
    }
}
