using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
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
              "description": "The URL, video link, or file reference."
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

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var scraper = scope.ServiceProvider.GetRequiredService<WebScraperService>();

        var draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId.Value && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
        if (draft == null)
        {
            return "Error: Draft not found or access denied.";
        }

        var sourceTitle = args.Title ?? args.Reference;
        var sourceContent = args.Content;

        if ((kind == SourceKind.Url || kind == SourceKind.YouTube) && string.IsNullOrEmpty(sourceContent))
        {
            try
            {
                var (scrapedTitle, scrapedContent, isYouTube) = await scraper.ScrapeUrlAsync(args.Reference);
                if (string.IsNullOrEmpty(args.Title))
                {
                    sourceTitle = scrapedTitle;
                }
                sourceContent = scrapedContent;
                if (isYouTube)
                {
                    kind = SourceKind.YouTube;
                }
            }
            catch (Exception ex)
            {
                sourceContent = $"Error scraping URL: {ex.Message}";
            }
        }

        var source = new Source
        {
            DraftId = draftId.Value,
            Kind = kind,
            Reference = args.Reference,
            Title = sourceTitle,
            Content = sourceContent
        };

        db.Sources.Add(source);
        draft.Status = DraftStatus.Editing;
        draft.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return $"Successfully added source '{source.Title}' ({source.Kind}) with ID {source.Id}.";
    }
}
