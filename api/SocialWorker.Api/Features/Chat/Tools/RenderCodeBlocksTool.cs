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

public sealed record RenderedCodeBlockItem(int Index, string Language, string MarkdownTag);

public sealed record RenderCodeBlocksResult(
    bool Success,
    IReadOnlyList<RenderedCodeBlockItem> RenderedBlocks,
    int TotalBlocks,
    string Message,
    string? Error = null) : IChatToolResult
{
    public static implicit operator string(RenderCodeBlocksResult result) => result.ToDisplayText();

    public string ToDisplayText()
    {
        return Message;
    }
}

public sealed class RenderCodeBlocksTool : ChatToolBase<RenderCodeBlocksArgs, RenderCodeBlocksResult>
{
    private readonly AppDbContext _db;
    private readonly CodeImageService _codeImageService;
    private readonly DraftsService _draftsService;

    public RenderCodeBlocksTool(
        AppDbContext db,
        CodeImageService codeImageService,
        DraftsService draftsService)
    {
        _db = db;
        _codeImageService = codeImageService;
        _draftsService = draftsService;
    }

    public override string Name => "render_code_blocks";
    public override string Description =>
        "Renders code blocks (triple-backtick fences) in the current draft as syntax-highlighted images and attaches them. " +
        "Use when the user wants to post code as a visual image (Carbon-style). " +
        "After rendering, the code fence is replaced with a compact ![code snippet](media://...) reference, which significantly reduces the post's character/word count and helps resolve character limit errors.";

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
              "description": "Optional 0-based index of a specific code block to render. If omitted, renders all code blocks."
            }
          }
        }
        """).RootElement.Clone();

    public override async Task<RenderCodeBlocksResult> ExecuteAsync(RenderCodeBlocksArgs args, Models.ToolExecutionContext context)
    {
        if (!context.DraftId.HasValue)
            return new RenderCodeBlocksResult(false, Array.Empty<RenderedCodeBlockItem>(), 0, "Error: No active draft.", "No active draft.");

        var draft = await _db.Drafts.FirstOrDefaultAsync(
            d => d.Id == context.DraftId.Value && d.UserId == context.UserId && d.Status != DraftStatus.Deleted, context.CancellationToken);
        if (draft == null)
            return new RenderCodeBlocksResult(false, Array.Empty<RenderedCodeBlockItem>(), 0, "Error: Draft not found or access denied.", "Draft not found or access denied.");

        var content = draft.Content ?? "";
        var blocks = CodeBlockParser.Parse(content);

        if (blocks.Count == 0)
            return new RenderCodeBlocksResult(false, Array.Empty<RenderedCodeBlockItem>(), 0, "No code blocks found in the draft.", "No code blocks found in the draft.");

        var theme = CodeTheme.FromString(args.Theme);

        var rendered = new List<(CodeBlock Block, string MarkdownTag, int Index)>();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (args.BlockIndex.HasValue && args.BlockIndex.Value != i) continue;

            var result = await _codeImageService.RenderAndStoreAsync(context.UserId, context.DraftId.Value, blocks[i], theme, context.CancellationToken);
            rendered.Add((blocks[i], result.MarkdownTag, i));
        }

        if (rendered.Count == 0)
            return new RenderCodeBlocksResult(
                false,
                Array.Empty<RenderedCodeBlockItem>(),
                blocks.Count,
                $"Block index {args.BlockIndex} not found. The draft has {blocks.Count} code block(s) (0-based).",
                "Requested code block index was not found.");

        content = ReplaceFencesWithImages(content, rendered);
        draft.Content = content;
        draft.UpdatedAt = DateTime.UtcNow;

        await _draftsService.ReconcileSegmentsAsync(draft, content, context.CancellationToken);
        await _db.SaveChangesAsync(context.CancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"Rendered {rendered.Count} code block(s) as image(s):");
        foreach (var (block, tag, idx) in rendered)
        {
            var lang = string.IsNullOrEmpty(block.Language) ? "plain" : block.Language;
            sb.AppendLine($"- Block {idx} ({lang}): {tag}");
        }

        var renderedItems = rendered
            .Select(item => new RenderedCodeBlockItem(
                item.Index,
                string.IsNullOrEmpty(item.Block.Language) ? "plain" : item.Block.Language,
                item.MarkdownTag))
            .ToList();

        return new RenderCodeBlocksResult(true, renderedItems, blocks.Count, sb.ToString().TrimEnd());
    }

    private static readonly Regex FenceRegex = new(
        @"```(\w*)\r?\n([\s\S]*?)```",
        RegexOptions.Compiled);

    private static string ReplaceFencesWithImages(
        string content,
        List<(CodeBlock Block, string MarkdownTag, int Index)> rendered)
    {
        var markdownByIndex = rendered.ToDictionary(r => r.Index, r => r.MarkdownTag);
        var matchIndex = 0;
        return FenceRegex.Replace(content, m =>
        {
            var currentIndex = matchIndex++;
            return markdownByIndex.TryGetValue(currentIndex, out var markdownTag) ? markdownTag : m.Value;
        });
    }
}
