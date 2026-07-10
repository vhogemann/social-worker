using System;
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

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Web search results for: '{args.Query}':\n");
        foreach (var r in results)
        {
            sb.AppendLine($"- **{r.Title}**");
            sb.AppendLine($"  URL: {r.Url}");
            sb.AppendLine($"  Snippet: {r.Snippet}\n");
        }

        return sb.ToString().Trim();
    }
}
