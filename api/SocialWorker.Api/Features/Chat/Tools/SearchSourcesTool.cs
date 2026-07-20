using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Features.Sources;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record SearchSourcesArgs(string Query, int? Limit);

public sealed record SearchSourcesResultItem(
    Guid Id,
    string Title,
    string Kind,
    string? Preview,
    string? Summary);

public sealed record SearchSourcesResult(IReadOnlyList<SearchSourcesResultItem> Items) : IChatToolResult
{
    public string ToDisplayText()
    {
        if (Items.Count == 0)
        {
            return "No matching sources found in the library.";
        }

        var lines = Items.Select(item =>
            $"- [{item.Kind}] {item.Title} (id: {item.Id}){(item.Summary != null ? $"\n  Summary: {item.Summary}" : "")}");
        return $"Found {Items.Count} source(s):\n{string.Join("\n", lines)}";
    }
}

public sealed class SearchSourcesTool : ChatToolBase<SearchSourcesArgs, SearchSourcesResult>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SearchSourcesTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override string Name => "search_sources";
    public override string Description => "Search your global source library for materials by keyword. Returns only sources NOT already linked to the active draft to avoid duplicates.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "query": {
              "type": "string",
              "description": "Keyword(s) to search for across source titles, content, and summaries."
            },
            "limit": {
              "type": "integer",
              "description": "Maximum number of results to return (1-20, default 5)."
            }
          },
          "required": ["query"]
        }
        """).RootElement.Clone();

    public override async Task<SearchSourcesResult> ExecuteAsync(SearchSourcesArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        var query = args.Query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchSourcesResult(Array.Empty<SearchSourcesResultItem>());
        }

        var limit = Math.Clamp(args.Limit ?? 5, 1, 20);

        using var scope = _scopeFactory.CreateScope();
        var sourcesService = scope.ServiceProvider.GetRequiredService<SourcesService>();

        var result = await sourcesService.SearchSourcesAsync(
            userId,
            query,
            page: 1,
            pageSize: limit,
            ct,
            excludeDraftId: draftId);

        var items = result.Items.Select(s => new SearchSourcesResultItem(
            s.Id,
            s.Title ?? s.Reference,
            s.Kind,
            s.Summary?.Length > 200 ? s.Summary[..200] + "..." : s.Summary,
            s.Summary)).ToList();

        return new SearchSourcesResult(items);
    }
}
