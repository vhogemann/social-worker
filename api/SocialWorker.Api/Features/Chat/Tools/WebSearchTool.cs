using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Infrastructure.Search;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record WebSearchArgs(string Query);

public sealed class WebSearchTool : ChatToolBase<WebSearchArgs, string>
{
    private readonly ISearchEngine _searchEngine;

    public WebSearchTool(ISearchEngine searchEngine)
    {
        _searchEngine = searchEngine;
    }

    public override string Name => "web_search";
    public override string Description => "Search the web for current information, facts, news, or articles.";

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

    public override async Task<string> ExecuteAsync(WebSearchArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Query))
        {
            return "No search query provided.";
        }

        var results = await _searchEngine.SearchAsync(args.Query, ct);
        if (results == null || results.Count == 0)
        {
            return "No search results found.";
        }

        var normalized = new List<object>();
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

            normalized.Add(new
            {
                rank = rank++,
                title = r.Title,
                url = uri.ToString(),
                snippet = r.Snippet
            });
        }

        if (normalized.Count == 0)
        {
            return "No valid absolute search result URLs found.";
        }

        var payload = new
        {
            query = args.Query,
            usageNotes = new[]
            {
                "Use the exact absolute URL from a result's url field when calling add_source.",
                "Do not pass relative paths, hostnames without scheme, or snippet text to add_source."
            },
            results = normalized
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
