using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Sources;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record AddSourceArgs(string Kind, string Reference, string? Title, string? Content);

public sealed class AddSourceTool : ChatToolBase<AddSourceArgs, string>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AddSourceTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override string Name => "add_source";
    public override string Description => "Add a web URL, YouTube video link, or document reference as a source for this draft.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "kind": {
              "type": "string",
              "enum": ["Url", "YouTube", "File"],
              "description": "The kind of the source."
            },
            "reference": {
              "type": "string",
                            "description": "The URL, video link, or file reference. For Url and YouTube kinds this must be an absolute HTTP or HTTPS URL."
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
          "required": ["kind", "reference"]
        }
        """).RootElement.Clone();

    public override async Task<string> ExecuteAsync(AddSourceArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (!draftId.HasValue)
        {
            return "Error: No draft ID active.";
        }

        if (!Enum.TryParse<SourceKind>(args.Kind, true, out var kind))
        {
            return $"Error: Invalid source kind '{args.Kind}'. Must be one of Url, YouTube, File.";
        }

        var reference = args.Reference?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reference))
        {
            return "Error: Source reference is required.";
        }

        if ((kind == SourceKind.Url || kind == SourceKind.YouTube) && !TryValidateAbsoluteHttpUrl(reference, out var referenceError))
        {
            return $"Error: {referenceError} Pass the exact absolute URL, including https://.";
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
                return "Error: The provided reference is not recognized as a YouTube URL.";
            }

            return $"Successfully added source '{result.Title}' ({result.Kind}) with ID {result.SourceId}. Use list_sources or fetch_source to inspect it before drafting from it.";
        }
        catch (KeyNotFoundException)
        {
            return "Error: Draft not found or access denied.";
        }
        catch (ArgumentException ex)
        {
            return $"Error: {ex.Message} Pass the exact absolute URL, including https://.";
        }
        catch (InvalidOperationException ex)
        {
            return $"Error: Failed to add source because the URL could not be scraped. {ex.Message}";
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
