using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Media;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record AddImageSourceArgs(string Url, string? AltText);

public sealed record AddImageSourceResult(
    bool Success,
    string Message,
    string? MarkdownTag = null,
    string? Error = null) : IChatToolResult
{
    public static implicit operator string(AddImageSourceResult result) => result.ToDisplayText();

    public string ToDisplayText()
    {
        return Success
            ? Message
            : $"Error: {Error ?? Message}";
    }
}

public sealed class AddImageSourceTool : ChatToolBase<AddImageSourceArgs, AddImageSourceResult>
{
    private readonly AppDbContext _db;
    private readonly MediaService _mediaService;
    private readonly IHttpClientFactory _httpClientFactory;

    public AddImageSourceTool(
        AppDbContext db,
        MediaService mediaService,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _mediaService = mediaService;
        _httpClientFactory = httpClientFactory;
    }

    public override string Name => "add_image_source";
    public override string Description => "Embeds an external image into the current draft by downloading, optimizing, and resizing it. It saves the asset internally and returns the required markdown link tag.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "url": {
              "type": "string",
              "description": "The absolute URL of the image to download."
            },
            "altText": {
              "type": "string",
              "description": "Optional alternative text describing the image."
            }
          },
          "required": ["url"]
        }
        """).RootElement.Clone();

    public override async Task<AddImageSourceResult> ExecuteAsync(AddImageSourceArgs args, Models.ToolExecutionContext context)
    {
        if (!context.DraftId.HasValue)
        {
            return new AddImageSourceResult(false, "No draft ID active.", Error: "No draft ID active.");
        }

        if (string.IsNullOrWhiteSpace(args.Url) || !Uri.TryCreate(args.Url, UriKind.Absolute, out _))
        {
            return new AddImageSourceResult(false, "A valid absolute image URL is required.", Error: "A valid absolute image URL is required.");
        }

        var draft = await _db.Drafts.FirstOrDefaultAsync(d => d.Id == context.DraftId.Value && d.UserId == context.UserId && d.Status != DraftStatus.Deleted, context.CancellationToken);
        if (draft == null)
        {
            return new AddImageSourceResult(false, "Draft not found or access denied.", Error: "Draft not found or access denied.");
        }

        try
        {
            using var client = _httpClientFactory.CreateClient();
            var uploadResult = await _mediaService.ImportMediaFromUrlAsync(
                context.UserId,
                context.DraftId.Value,
                args.Url,
                client,
                context.CancellationToken,
                args.AltText
            );

            var finalTag = !string.IsNullOrWhiteSpace(args.AltText) 
                ? $"![{args.AltText}](media://{uploadResult.Id})"
                : uploadResult.MarkdownTag;

            return new AddImageSourceResult(true, $"Successfully imported image. Markdown tag: {finalTag}", finalTag);
        }
        catch (Exception ex)
        {
            return new AddImageSourceResult(false, $"Error importing image: {ex.Message}", Error: ex.Message);
        }
    }
}
