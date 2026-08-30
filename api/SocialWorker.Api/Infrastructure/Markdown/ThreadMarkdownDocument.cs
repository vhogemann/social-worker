using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;

namespace SocialWorker.Api.Infrastructure.Markdown;

public sealed class ThreadMarkdownDocument
{
    private static readonly Regex SetextDividerRegex = new(@"(?m)(^[^\n\r]+)\n(---|___|\*\*\*)(\s*)$", RegexOptions.Compiled);

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation()
        .Build();

    public string RawMarkdown { get; }
    public IReadOnlyList<ThreadSegmentDocument> Segments { get; }

    public ThreadMarkdownDocument(string rawMarkdown, IReadOnlyList<ThreadSegmentDocument> segments)
    {
        RawMarkdown = rawMarkdown;
        Segments = segments;
    }

    public static ThreadMarkdownDocument Parse(string? markdown)
    {
        var rawMarkdown = markdown ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawMarkdown))
        {
            return new ThreadMarkdownDocument(
                rawMarkdown,
                new[] { ThreadSegmentDocument.CreateFromBlocks(Array.Empty<Block>(), string.Empty) });
        }

        var normalizedMarkdown = SetextDividerRegex.Replace(rawMarkdown.Replace("\r\n", "\n"), "$1\n\n$2$3");
        var doc = Markdig.Markdown.Parse(normalizedMarkdown, Pipeline);

        var segments = new List<ThreadSegmentDocument>();
        var currentBlocks = new List<Block>();
        int lastSegmentEndChar = 0;

        foreach (var block in doc)
        {
            if (block is ThematicBreakBlock thematicBreak)
            {
                var segmentSpanEnd = thematicBreak.Span.Start;
                var rawSegmentSlice = normalizedMarkdown.Substring(lastSegmentEndChar, Math.Max(0, segmentSpanEnd - lastSegmentEndChar));

                segments.Add(ThreadSegmentDocument.CreateFromBlocks(currentBlocks, rawSegmentSlice));
                currentBlocks.Clear();
                lastSegmentEndChar = thematicBreak.Span.End + 1;
            }
            else
            {
                currentBlocks.Add(block);
            }
        }

        var tailSlice = lastSegmentEndChar < normalizedMarkdown.Length
            ? normalizedMarkdown.Substring(lastSegmentEndChar)
            : string.Empty;

        segments.Add(ThreadSegmentDocument.CreateFromBlocks(currentBlocks, tailSlice));

        if (segments.Count == 0)
        {
            segments.Add(ThreadSegmentDocument.CreateFromBlocks(Array.Empty<Block>(), string.Empty));
        }

        return new ThreadMarkdownDocument(rawMarkdown, segments);
    }
}

