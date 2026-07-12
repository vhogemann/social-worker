using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.CodeImages;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Media;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record RenderCodeBlocksArgs(string? Theme, int? BlockIndex);

public sealed class RenderCodeBlocksTool : ChatToolBase<RenderCodeBlocksArgs, string>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RenderCodeBlocksTool(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public override string Name => "render_code_blocks";
    public override string Description =>
        "Renders code blocks (triple-backtick fences) in the current draft as syntax-highlighted images and attaches them. " +
        "Use when the user wants to post code as a visual image (Carbon-style). " +
        "After rendering, the code fence is replaced with a ![code snippet](media://...) reference.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "theme": {
              "type": "string",
              "enum": ["Dark", "Light"],
              "description": "Visual theme for the code image. Defaults to Dark."
            },
            "blockIndex": {
              "type": "integer",
              "description": "Zero-based index of the code block to render. If omitted, all code blocks in the draft are rendered."
            }
          }
        }
        """).RootElement.Clone();

    public override async Task<string> ExecuteAsync(RenderCodeBlocksArgs args, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (!draftId.HasValue)
            return "Error: No active draft.";

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();
        var renderer = scope.ServiceProvider.GetRequiredService<CodeImageRenderer>();
        var draftsService = scope.ServiceProvider.GetRequiredService<DraftsService>();

        var draft = await db.Drafts.FirstOrDefaultAsync(
            d => d.Id == draftId.Value && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
        if (draft == null)
            return "Error: Draft not found or access denied.";

        var content = draft.Content ?? "";
        var blocks = CodeBlockParser.Parse(content);

        if (blocks.Count == 0)
            return "No code blocks found in the draft.";

        var theme = CodeTheme.FromString(args.Theme);
        var codeImageService = new CodeImageService(mediaService, renderer);

        var rendered = new List<(CodeBlock Block, string MarkdownTag, int Index)>();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (args.BlockIndex.HasValue && args.BlockIndex.Value != i) continue;

            var result = await codeImageService.RenderAndStoreAsync(userId, draftId.Value, blocks[i], theme, ct);
            rendered.Add((blocks[i], result.MarkdownTag, i));
        }

        if (rendered.Count == 0)
            return $"Block index {args.BlockIndex} not found. The draft has {blocks.Count} code block(s) (0-based).";

        content = ReplaceFencesWithImages(content, rendered);
        draft.Content = content;
        draft.UpdatedAt = DateTime.UtcNow;

        await draftsService.ReconcileSegmentsAsync(draft, content, ct);
        await db.SaveChangesAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine($"Rendered {rendered.Count} code block(s) as image(s):");
        foreach (var (block, tag, idx) in rendered)
        {
            var lang = string.IsNullOrEmpty(block.Language) ? "plain" : block.Language;
            sb.AppendLine($"- Block {idx} ({lang}): {tag}");
        }
        return sb.ToString().TrimEnd();
    }

    private static readonly Regex FenceRegex = new(
        @"```(\w*)\r?\n([\s\S]*?)```",
        RegexOptions.Compiled);

    private static string ReplaceFencesWithImages(
        string content,
        List<(CodeBlock Block, string MarkdownTag, int Index)> rendered)
    {
        var matchIndex = 0;
        return FenceRegex.Replace(content, m =>
        {
            var currentIndex = matchIndex++;
            var hit = rendered.Find(r => r.Index == currentIndex);
            return hit.MarkdownTag != null ? hit.MarkdownTag : m.Value;
        });
    }
}
