using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Infrastructure.Search;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record WebSearchArgs(string Query);

public sealed record WebSearchResultItem(int Rank, string Title, string Url, string? Snippet);

public sealed record WebSearchResult(
    string Query,
    IReadOnlyList<string> UsageNotes,
    IReadOnlyList<WebSearchResultItem> Results,
    string? Error = null) : IChatToolResult
{
    public static implicit operator string(WebSearchResult result) => result.ToDisplayText();

    public string ToDisplayText()
    {
        if (!string.IsNullOrWhiteSpace(Error))
        {
            return Error;
        }

        var payload = new
        {
            query = Query,
            usageNotes = UsageNotes,
            results = Results.Select(item => new
            {
                rank = item.Rank,
                title = item.Title,
                url = item.Url,
                snippet = item.Snippet
            })
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}

public sealed class WebSearchTool : ChatToolBase<WebSearchArgs, WebSearchResult>
{
    private readonly ISearchEngine _searchEngine;

    public WebSearchTool(ISearchEngine searchEngine)
    {
        _searchEngine = searchEngine;
    }

    public override string Name => "web_search";
    public override string Description => "Use this tool to retrieve up-to-date information, facts, news articles, or statistics from the live web. Call it whenever you need to check recent events.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "query": {
              "type": "string",
              "description": "The search term or query to execute."
            }
          },
          "required": ["query"]
        }
        """).RootElement.Clone();

    public override async Task<WebSearchResult> ExecuteAsync(WebSearchArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Query))
        {
            return new WebSearchResult(string.Empty, Array.Empty<string>(), Array.Empty<WebSearchResultItem>(), "No search query provided.");
        }

        var results = await _searchEngine.SearchAsync(args.Query, ct);
        if (results == null || results.Count == 0)
        {
            return new WebSearchResult(args.Query, Array.Empty<string>(), Array.Empty<WebSearchResultItem>(), "No search results found.");
        }

        var normalized = new List<WebSearchResultItem>();
        var rank = 1;
        foreach (var r in results)
        {
            if (!Uri.TryCreate(r.Url, UriKind.Absolute, out var uri))
            {
                continue;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                continue;
            }

            normalized.Add(new WebSearchResultItem(rank++, r.Title, uri.ToString(), r.Snippet));
        }

        if (normalized.Count == 0)
        {
            return new WebSearchResult(args.Query, Array.Empty<string>(), Array.Empty<WebSearchResultItem>(), "No valid absolute search result URLs found.");
        }

        return new WebSearchResult(
            args.Query,
            new[]
            {
                "Use the exact absolute URL from a result's url field when calling add_source.",
                "Do not pass relative paths, hostnames without scheme, or snippet text to add_source."
            },
            normalized);
    }
}
