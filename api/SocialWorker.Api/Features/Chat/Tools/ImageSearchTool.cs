using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Infrastructure.Search;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record ImageSearchArgs(string Query);

public sealed class ImageSearchTool : ChatToolBase<ImageSearchArgs, string>
{
    private const int MaxResults = 12;
    private readonly ISearchEngine _searchEngine;

    public ImageSearchTool(ISearchEngine searchEngine)
    {
        _searchEngine = searchEngine;
    }

    public override string Name => "image_search";
    public override string Description => "Search the web for images and return compact candidate URLs. For visual inspection, import a candidate via add_image_source first, then call view_image with media://{guid}.";

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
        sb.AppendLine($"Image search results for: '{args.Query}' (top {Math.Min(MaxResults, results.Count)}):");
        sb.AppendLine();

        var count = 0;
        foreach (var r in results)
        {
            count++;
            if (count > MaxResults)
            {
                break;
            }

            sb.AppendLine($"{count}. {r.Title}");
            sb.AppendLine($"   URL: {r.Url}");
            if (!string.IsNullOrEmpty(r.Snippet))
            {
                sb.AppendLine($"   Description: {r.Snippet}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Next step: call add_image_source with one direct image URL, then call view_image with the returned media://{guid}.");

        return sb.ToString().Trim();
    }
}
