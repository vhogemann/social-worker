using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Infrastructure.Search;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record ImageSearchArgs(string Query);

public sealed record ImageSearchResultItem(string Title, string Url, string? Description);

public sealed record ImageSearchResult(
    string Query,
    IReadOnlyList<ImageSearchResultItem> Results,
    IReadOnlyList<string> UsageNotes,
    string? Error = null) : IChatToolResult
{
    public static implicit operator string(ImageSearchResult result) => result.ToDisplayText();

    public string ToDisplayText()
    {
        if (!string.IsNullOrWhiteSpace(Error))
        {
            return Error;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Image search results for: '{Query}' (top {Results.Count}):");
        sb.AppendLine();

        for (var i = 0; i < Results.Count; i++)
        {
            var item = Results[i];
            sb.AppendLine($"{i + 1}. {item.Title}");
            sb.AppendLine($"   URL: {item.Url}");
            if (!string.IsNullOrEmpty(item.Description))
            {
                sb.AppendLine($"   Description: {item.Description}");
            }
            sb.AppendLine();
        }

        foreach (var note in UsageNotes)
        {
            sb.AppendLine(note);
        }

        return sb.ToString().Trim();
    }
}

public sealed class ImageSearchTool : ChatToolBase<ImageSearchArgs, ImageSearchResult>
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

    public override async Task<ImageSearchResult> ExecuteAsync(ImageSearchArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(args.Query))
        {
            return new ImageSearchResult(string.Empty, Array.Empty<ImageSearchResultItem>(), Array.Empty<string>(), "No image search query provided.");
        }

        var results = await _searchEngine.SearchImagesAsync(args.Query, ct);
        if (results == null || results.Count == 0)
        {
            return new ImageSearchResult(args.Query, Array.Empty<ImageSearchResultItem>(), Array.Empty<string>(), "No image search results found.");
        }

        var normalized = new List<ImageSearchResultItem>();
        foreach (var r in results)
        {
            if (normalized.Count >= MaxResults)
            {
                break;
            }

            normalized.Add(new ImageSearchResultItem(r.Title, r.Url, r.Snippet));
        }

        return new ImageSearchResult(
            args.Query,
            normalized,
            new[]
            {
                "Next step: call add_image_source with one direct image URL, then call view_image with the returned media://{guid}."
            });
    }
}
