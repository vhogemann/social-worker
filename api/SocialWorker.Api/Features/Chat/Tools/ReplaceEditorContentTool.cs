using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record ReplaceEditorContentArgs(string Markdown);

public sealed record ReplaceEditorContentResult(bool Success, int Length, string Content) : IChatToolResult
{
  public string ToDisplayText()
  {
    return Success
      ? $"Editor content replaced ({Length} chars)."
      : "Failed to replace editor content.";
  }
}

public sealed class ReplaceEditorContentTool : ChatToolBase<ReplaceEditorContentArgs, ReplaceEditorContentResult>
{
    private readonly AppDbContext _db;
    private readonly DraftsService _draftsService;

    public ReplaceEditorContentTool(AppDbContext db, DraftsService draftsService)
    {
        _db = db;
        _draftsService = draftsService;
    }

    public override string Name => "replace_editor_content";
    public override string Description => "Completely overwrites all existing content in the markdown editor with the provided text string. Use this when the entire document needs to be replaced.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "markdown": {
              "type": "string",
              "description": "The full markdown content to replace the editor with. Use --- on its own line to separate thread segments."
            }
          },
          "required": ["markdown"]
        }
        """).RootElement.Clone();

    public override async Task<ReplaceEditorContentResult> ExecuteAsync(ReplaceEditorContentArgs args, Models.ToolExecutionContext context)
    {
        var markdown = args.Markdown;

        var draft = context.DraftId.HasValue
            ? await _db.Drafts.FirstOrDefaultAsync(d => d.Id == context.DraftId.Value && d.UserId == context.UserId && d.Status != DraftStatus.Deleted, context.CancellationToken)
                ?? throw new InvalidOperationException($"Draft {context.DraftId.Value} not found or access denied")
            : await _db.Drafts.OrderByDescending(d => d.UpdatedAt).FirstOrDefaultAsync(d => d.UserId == context.UserId && d.Status != DraftStatus.Deleted, context.CancellationToken)
                ?? throw new InvalidOperationException("No active draft found");

        draft.Content = markdown;
        draft.UpdatedAt = DateTime.UtcNow;

        await _draftsService.ReconcileSegmentsAsync(draft, markdown, context.CancellationToken);
        await _db.SaveChangesAsync(context.CancellationToken);

        return new ReplaceEditorContentResult(true, markdown.Length, markdown);
    }
}
