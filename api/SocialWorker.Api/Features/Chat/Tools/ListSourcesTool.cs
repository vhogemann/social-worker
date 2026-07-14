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

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record ListSourcesArgs;

public sealed record ListSourcesResultItem(Guid Id, string Kind, string Reference, string? Title);

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

        var sources = await db.Sources
            .Where(s => s.DraftSources.Any(ds => ds.DraftId == activeDraftId.Value))
            .Select(s => new ListSourcesResultItem(s.Id, s.Kind.ToString(), s.Reference, s.Title))
            .ToListAsync(ct);

        return sources;
    }
}
