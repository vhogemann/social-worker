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
    private readonly IServiceScopeFactory _scopeFactory;

    public ReplaceEditorContentTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
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

    public override async Task<ReplaceEditorContentResult> ExecuteAsync(ReplaceEditorContentArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        var markdown = args.Markdown;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var draftsService = scope.ServiceProvider.GetRequiredService<DraftsService>();

        var draft = draftId.HasValue
            ? await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId.Value && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
                ?? throw new InvalidOperationException($"Draft {draftId.Value} not found or access denied")
            : await db.Drafts.OrderByDescending(d => d.UpdatedAt).FirstOrDefaultAsync(d => d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
                ?? throw new InvalidOperationException("No active draft found");

        draft.Content = markdown;
        draft.UpdatedAt = DateTime.UtcNow;

        await draftsService.ReconcileSegmentsAsync(draft, markdown, ct);
        await db.SaveChangesAsync(ct);

        return new ReplaceEditorContentResult(true, markdown.Length, markdown);
    }
}
