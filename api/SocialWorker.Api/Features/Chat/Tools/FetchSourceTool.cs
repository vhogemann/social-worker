using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Sources;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record FetchSourceArgs(string Id);

public sealed record FetchSourceResult(
    Guid Id,
    string Kind,
    string Reference,
    string? Title,
    string? Content,
    string ProcessingStatus,
    string? CanonicalUrl = null,
    string? CitationLabel = null,
    string? EmbedKind = null,
    string? CanonicalEmbedMarkdown = null,
    string? PlainLinkLine = null) : IChatToolResult
{
    public string ToDisplayText()
    {
        return $"Fetched source {Id} ({Kind}, status: {ProcessingStatus}): {Title ?? Reference}";
    }
}

public sealed class FetchSourceTool : ChatToolBase<FetchSourceArgs, FetchSourceResult>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public FetchSourceTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override string Name => "fetch_source";
    public override string Description => "Fetch the full text content and metadata of a specific source by Guid ID. If processingStatus is not 'Complete', the content field is not yet populated.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "id": {
              "type": "string",
              "description": "The unique Guid identifier of the source to read."
            }
          },
          "required": ["id"]
        }
        """).RootElement.Clone();

    public override async Task<FetchSourceResult> ExecuteAsync(FetchSourceArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        var sourceIdStr = args.Id;
        if (sourceIdStr.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            sourceIdStr = sourceIdStr.Substring("file://".Length);
        }
        else if (sourceIdStr.StartsWith("media://", StringComparison.OrdinalIgnoreCase))
        {
            sourceIdStr = sourceIdStr.Substring("media://".Length);
        }

        if (!Guid.TryParse(sourceIdStr, out var sourceId))
        {
            throw new ArgumentException("Invalid Guid ID format");
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var source = await db.Sources.FirstOrDefaultAsync(s => s.Id == sourceId, ct);
        if (source == null)
        {
            throw new InvalidOperationException($"Source {sourceId} not found");
        }

        var owned = await db.DraftSources
            .AnyAsync(ds => ds.SourceId == source.Id && ds.Draft.UserId == userId && ds.Draft.Status != DraftStatus.Deleted, ct);
        if (!owned)
        {
            throw new UnauthorizedAccessException("Access denied to target source");
        }

        var links = SourceLinkFields.Build(source.Id, source.Kind, source.Reference, source.Title);
        return new FetchSourceResult(
            source.Id,
            source.Kind.ToString(),
            source.Reference,
            source.Title,
            source.Content,
            source.ProcessingStatus.ToString(),
            links.CanonicalUrl,
            links.CitationLabel,
            links.EmbedKind,
            links.CanonicalEmbedMarkdown,
            links.PlainLinkLine);
    }
}
