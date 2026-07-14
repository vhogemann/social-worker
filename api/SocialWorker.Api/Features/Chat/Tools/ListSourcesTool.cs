using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Sources;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record ListSourcesArgs;

public sealed record ListSourcesResultItem(
    Guid Id,
    string Kind,
    string Reference,
    string? Title,
    string? CanonicalUrl = null,
    string? CitationLabel = null,
    string? EmbedKind = null,
    string? CanonicalEmbedMarkdown = null,
    string? PlainLinkLine = null);

public sealed class ListSourcesTool : ChatToolBase<ListSourcesArgs, List<ListSourcesResultItem>>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ListSourcesTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override string Name => "list_sources";
    public override string Description => "List all sources attached to the active draft (e.g. text notes or URLs parsed from the text).";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {},
          "required": []
        }
        """).RootElement.Clone();

    public override async Task<List<ListSourcesResultItem>> ExecuteAsync(ListSourcesArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var activeDraftId = draftId.HasValue
            ? draftId.Value
            : (await db.Drafts.OrderByDescending(d => d.UpdatedAt).FirstOrDefaultAsync(d => d.UserId == userId && d.Status != DraftStatus.Deleted, ct))?.Id;

        if (activeDraftId == null)
        {
            return new List<ListSourcesResultItem>();
        }

        var rows = await db.Sources
            .Where(s => s.DraftSources.Any(ds => ds.DraftId == activeDraftId.Value))
            .ToListAsync(ct);

        var sources = rows.Select(s =>
        {
            var links = SourceLinkFields.Build(s.Id, s.Kind, s.Reference, s.Title);
            return new ListSourcesResultItem(
                s.Id,
                s.Kind.ToString(),
                s.Reference,
                s.Title,
                links.CanonicalUrl,
                links.CitationLabel,
                links.EmbedKind,
                links.CanonicalEmbedMarkdown,
                links.PlainLinkLine);
        }).ToList();

        return sources;
    }
}
