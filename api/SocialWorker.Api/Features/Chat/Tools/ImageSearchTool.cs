using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Infrastructure.Search;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record ImageSearchArgs(string Query);

public sealed class ImageSearchTool : ChatToolBase<ImageSearchArgs, string>
{
    private readonly ISearchEngine _searchEngine;

    public ImageSearchTool(ISearchEngine searchEngine)
    {
        _searchEngine = searchEngine;
    }

    public override string Name => "image_search";
    public override string Description => "Search the web specifically for images/pictures and return direct image URLs.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "query": {
              "type": "string",
              "description": "The search term or keyword to find images for."
            }
          },
          "required": ["query"]
        }
        """).RootElement.Clone();

    public override async Task<string> ExecuteAsync(ImageSearchArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Query))
        {
            return "No image search query provided.";
        }

        var results = await _searchEngine.SearchImagesAsync(args.Query, ct);
        if (results == null || results.Count == 0)
        {
            return "No image search results found.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Image search results for: '{args.Query}':\n");
        foreach (var r in results)
        {
            sb.AppendLine($"- **{r.Title}**");
            sb.AppendLine($"  URL: {r.Url}");
            if (!string.IsNullOrEmpty(r.Snippet))
            {
                sb.AppendLine($"  Description: {r.Snippet}");
            }
            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }
}
