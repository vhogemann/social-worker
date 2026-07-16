using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Sources;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record AddSourceArgs(string? Kind, string? Reference, string? Title, string? Content, Guid? SourceId);

public sealed record AddSourceResult(
    bool Success,
    string Message,
    Guid? SourceId = null,
    string? Kind = null,
    string? Error = null) : IChatToolResult
{
    public static implicit operator string(AddSourceResult result) => result.ToDisplayText();

    public string ToDisplayText()
    {
        return Success
            ? Message
            : $"Error: {Error ?? Message}";
    }
}

public sealed class AddSourceTool : ChatToolBase<AddSourceArgs, AddSourceResult>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AddSourceTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override string Name => "add_source";
    public override string Description => "Add a source to this draft. Either provide a new URL/YouTube/File reference, or pass an existing sourceId (from search_sources) to link an existing source without duplicating it.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "source_id": {
              "type": "string",
              "description": "ID of an existing source to link to this draft (from search_sources results). When provided, kind and reference are not required."
            },
            "kind": {
              "type": "string",
              "enum": ["Url", "YouTube", "File"],
              "description": "The kind of the source. Required when adding a new source."
            },
            "reference": {
              "type": "string",
              "description": "The URL, video link, or file reference. Required when adding a new source. For Url and YouTube kinds this must be an absolute HTTP or HTTPS URL."
            },
            "title": {
              "type": "string",
              "description": "Optional title for the source."
            },
            "content": {
              "type": "string",
              "description": "Optional text content or transcript."
            }
          },
          "required": []
        }
        """).RootElement.Clone();

    public override async Task<AddSourceResult> ExecuteAsync(AddSourceArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (!draftId.HasValue)
        {
            return new AddSourceResult(false, "No draft ID active.", Error: "No draft ID active.");
        }

        if (args.SourceId.HasValue)
        {
            using var linkScope = _scopeFactory.CreateScope();
            var linkService = linkScope.ServiceProvider.GetRequiredService<SourcesService>();
            try
            {
                var linked = await linkService.LinkSourceAsync(userId, args.SourceId.Value, draftId.Value, ct);
                return new AddSourceResult(
                    true,
                    $"Linked existing source '{linked.Title}' ({linked.Kind}) to this draft.",
                    linked.Id,
                    linked.Kind);
            }
            catch (KeyNotFoundException)
            {
                return new AddSourceResult(false, "Source or draft not found.", Error: "Source or draft not found.");
            }
        }

        if (string.IsNullOrWhiteSpace(args.Kind))
        {
            return new AddSourceResult(false, "Either source_id or kind+reference is required.", Error: "Either source_id or kind+reference is required.");
        }

        if (!Enum.TryParse<SourceKind>(args.Kind, true, out var kind))
        {
            var error = $"Invalid source kind '{args.Kind}'. Must be one of Url, YouTube, File.";
            return new AddSourceResult(false, error, Error: error);
        }

        var reference = args.Reference?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return new AddSourceResult(false, "Source reference is required.", Error: "Source reference is required.");
        }

        if ((kind == SourceKind.Url || kind == SourceKind.YouTube) && !TryValidateAbsoluteHttpUrl(reference, out var referenceError))
        {
            var error = $"{referenceError} Pass the exact absolute URL, including https://.";
            return new AddSourceResult(false, error, Error: error);
        }

        using var scope = _scopeFactory.CreateScope();
        var sourcesService = scope.ServiceProvider.GetRequiredService<SourcesService>();

        try
        {
            var result = await sourcesService.AddUrlSourceAsync(
                userId,
                draftId.Value,
                reference,
                args.Title,
                args.Content,
                ct);

            if (kind == SourceKind.YouTube && !string.Equals(result.Kind, "YouTube", StringComparison.OrdinalIgnoreCase))
            {
                return new AddSourceResult(false, "The provided reference is not recognized as a YouTube URL.", Error: "The provided reference is not recognized as a YouTube URL.");
            }

            return new AddSourceResult(
                true,
                $"Successfully added source '{result.Title}' ({result.Kind}) with ID {result.SourceId}. Use list_sources or fetch_source to inspect it before drafting from it.",
                result.SourceId,
                result.Kind);
        }
        catch (KeyNotFoundException)
        {
            return new AddSourceResult(false, "Draft not found or access denied.", Error: "Draft not found or access denied.");
        }
        catch (ArgumentException ex)
        {
            return new AddSourceResult(false, $"{ex.Message} Pass the exact absolute URL, including https://.", Error: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new AddSourceResult(false, $"Failed to add source because the URL could not be scraped. {ex.Message}", Error: ex.Message);
        }
    }

    private static bool TryValidateAbsoluteHttpUrl(string reference, out string error)
    {
        error = string.Empty;
        if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri))
        {
            error = "Source URLs must be absolute HTTP or HTTPS URLs.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Source URLs must use http:// or https://.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            error = "Source URLs must include a valid host.";
            return false;
        }

        return true;
    }
}
