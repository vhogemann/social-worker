using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace SocialWorker.Api.Infrastructure.Markdown;

public sealed class ThreadSegmentDocument
{
    private static readonly Regex HashtagRegex = new(@"#([a-zA-Z][a-zA-Z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex BareUrlRegex = new(@"https?://[^\s\])>""\n]+", RegexOptions.Compiled);

    public string RawMarkdown { get; }
    public string PlainText { get; }
    public int CharacterCount => PlainText.Length;

    public IReadOnlyList<MediaEmbedReference> MediaEmbeds { get; }
    public YouTubeEmbedReference? YouTubeEmbed => AllYouTubeEmbeds.FirstOrDefault();
    public IReadOnlyList<YouTubeEmbedReference> AllYouTubeEmbeds { get; }
    public IReadOnlyList<LinkReference> TextLinks { get; }
    public IReadOnlyList<HashtagReference> Hashtags { get; }
    public IReadOnlyList<BareUrlReference> BareUrls { get; }
    public bool HasYouTube => YouTubeEmbed != null || TextLinks.Any(l => IsYouTubeUrl(l.Url)) || BareUrls.Any(b => IsYouTubeUrl(b.Url)) || IsYouTubeUrl(PlainText);
    public bool HasUnsupportedFormatting { get; }
    public IReadOnlyList<string> UnsupportedFormattingDetails { get; }

    public ThreadSegmentDocument(
        string rawMarkdown,
        string plainText,
        IReadOnlyList<MediaEmbedReference> mediaEmbeds,
        IReadOnlyList<YouTubeEmbedReference> youtubeEmbeds,
        IReadOnlyList<LinkReference> textLinks,
        IReadOnlyList<HashtagReference> hashtags,
        IReadOnlyList<BareUrlReference> bareUrls,
        bool hasUnsupportedFormatting,
        IReadOnlyList<string> unsupportedDetails)
    {
        RawMarkdown = rawMarkdown;
        PlainText = plainText;
        MediaEmbeds = mediaEmbeds;
        AllYouTubeEmbeds = youtubeEmbeds;
        TextLinks = textLinks;
        Hashtags = hashtags;
        BareUrls = bareUrls;
        HasUnsupportedFormatting = hasUnsupportedFormatting;
        UnsupportedFormattingDetails = unsupportedDetails;
    }

    public static ThreadSegmentDocument CreateFromBlocks(IReadOnlyList<Block> blocks, string rawMarkdown)
    {
        if (blocks.Count == 0 || string.IsNullOrWhiteSpace(rawMarkdown))
        {
            return new ThreadSegmentDocument(
                rawMarkdown: string.Empty,
                plainText: string.Empty,
                mediaEmbeds: Array.Empty<MediaEmbedReference>(),
                youtubeEmbeds: Array.Empty<YouTubeEmbedReference>(),
                textLinks: Array.Empty<LinkReference>(),
                hashtags: Array.Empty<HashtagReference>(),
                bareUrls: Array.Empty<BareUrlReference>(),
                hasUnsupportedFormatting: false,
                unsupportedDetails: Array.Empty<string>());
        }

        var mediaEmbeds = new List<MediaEmbedReference>();
        var youtubeEmbeds = new List<YouTubeEmbedReference>();
        var textLinks = new List<LinkReference>();
        var unsupportedDetails = new List<string>();
        var plainTextBuilder = new StringBuilder();

        for (int b = 0; b < blocks.Count; b++)
        {
            var block = blocks[b];
            ProcessBlock(block, plainTextBuilder, mediaEmbeds, youtubeEmbeds, textLinks, unsupportedDetails);

            if (b < blocks.Count - 1 && plainTextBuilder.Length > 0 && !plainTextBuilder.ToString().EndsWith("\n\n"))
            {
                if (plainTextBuilder.ToString().EndsWith("\n"))
                {
                    plainTextBuilder.Append('\n');
                }
                else
                {
                    plainTextBuilder.Append("\n\n");
                }
            }
        }

        var plainText = plainTextBuilder.ToString().Trim();

        var usedByteRanges = new HashSet<(int Start, int End)>();
        foreach (var link in textLinks)
        {
            usedByteRanges.Add((link.PlainTextByteStart, link.PlainTextByteEnd));
        }

        var hashtags = new List<HashtagReference>();
        foreach (Match match in HashtagRegex.Matches(plainText))
        {
            var byteStart = Encoding.UTF8.GetByteCount(plainText.Substring(0, match.Index));
            var byteEnd = byteStart + Encoding.UTF8.GetByteCount(match.Value);

            if (!OverlapsAny(usedByteRanges, byteStart, byteEnd))
            {
                hashtags.Add(new HashtagReference(match.Groups[1].Value, byteStart, byteEnd));
                usedByteRanges.Add((byteStart, byteEnd));
            }
        }

        var bareUrls = new List<BareUrlReference>();
        foreach (Match match in BareUrlRegex.Matches(plainText))
        {
            var byteStart = Encoding.UTF8.GetByteCount(plainText.Substring(0, match.Index));
            var byteEnd = byteStart + Encoding.UTF8.GetByteCount(match.Value);

            if (!OverlapsAny(usedByteRanges, byteStart, byteEnd))
            {
                bareUrls.Add(new BareUrlReference(match.Value, byteStart, byteEnd));
                usedByteRanges.Add((byteStart, byteEnd));
            }
        }

        return new ThreadSegmentDocument(
            rawMarkdown: rawMarkdown.Trim(),
            plainText: plainText,
            mediaEmbeds: mediaEmbeds,
            youtubeEmbeds: youtubeEmbeds,
            textLinks: textLinks,
            hashtags: hashtags,
            bareUrls: bareUrls,
            hasUnsupportedFormatting: unsupportedDetails.Count > 0,
            unsupportedDetails: unsupportedDetails);
    }

    private static void ProcessBlock(
        Block block,
        StringBuilder plainTextBuilder,
        List<MediaEmbedReference> mediaEmbeds,
        List<YouTubeEmbedReference> youtubeEmbeds,
        List<LinkReference> textLinks,
        List<string> unsupportedDetails)
    {
        switch (block)
        {
            case HeadingBlock heading:
                unsupportedDetails.Add($"Heading level {heading.Level}");
                if (heading.Inline != null)
                {
                    ProcessInlines(heading.Inline, plainTextBuilder, mediaEmbeds, youtubeEmbeds, textLinks, unsupportedDetails);
                }
                break;

            case ParagraphBlock paragraph:
                if (paragraph.Inline != null)
                {
                    ProcessInlines(paragraph.Inline, plainTextBuilder, mediaEmbeds, youtubeEmbeds, textLinks, unsupportedDetails);
                }
                break;

            case FencedCodeBlock fencedCode:
                foreach (var line in fencedCode.Lines.Lines)
                {
                    if (line.Slice.Text != null)
                    {
                        plainTextBuilder.AppendLine(line.Slice.ToString());
                    }
                }
                break;

            case CodeBlock codeBlock:
                foreach (var line in codeBlock.Lines.Lines)
                {
                    if (line.Slice.Text != null)
                    {
                        plainTextBuilder.AppendLine(line.Slice.ToString());
                    }
                }
                break;

            case ListBlock listBlock:
                foreach (var item in listBlock)
                {
                    if (item is ListItemBlock listItem)
                    {
                        plainTextBuilder.Append("• ");
                        foreach (var inner in listItem)
                        {
                            ProcessBlock(inner, plainTextBuilder, mediaEmbeds, youtubeEmbeds, textLinks, unsupportedDetails);
                        }
                    }
                }
                break;

            case ContainerBlock container:
                foreach (var child in container)
                {
                    ProcessBlock(child, plainTextBuilder, mediaEmbeds, youtubeEmbeds, textLinks, unsupportedDetails);
                }
                break;
        }
    }

    private static void ProcessInlines(
        ContainerInline container,
        StringBuilder plainTextBuilder,
        List<MediaEmbedReference> mediaEmbeds,
        List<YouTubeEmbedReference> youtubeEmbeds,
        List<LinkReference> textLinks,
        List<string> unsupportedDetails)
    {
        foreach (var inline in container)
        {
            ProcessInline(inline, plainTextBuilder, mediaEmbeds, youtubeEmbeds, textLinks, unsupportedDetails);
        }
    }

    private static void ProcessInline(
        Inline inline,
        StringBuilder plainTextBuilder,
        List<MediaEmbedReference> mediaEmbeds,
        List<YouTubeEmbedReference> youtubeEmbeds,
        List<LinkReference> textLinks,
        List<string> unsupportedDetails)
    {
        switch (inline)
        {
            case LinkInline link:
                var url = link.Url ?? string.Empty;
                var altOrLabel = ExtractInlineText(link);

                if (link.IsImage)
                {
                    if (url.StartsWith("media://", StringComparison.OrdinalIgnoreCase))
                    {
                        var idString = url.Substring("media://".Length).Trim();
                        if (Guid.TryParse(idString, out var mediaId))
                        {
                            mediaEmbeds.Add(new MediaEmbedReference(mediaId, altOrLabel, link.Span.Start, link.Span.End));
                        }
                    }
                    else if (IsYouTubeUrl(url))
                    {
                        var title = !string.IsNullOrWhiteSpace(altOrLabel) ? altOrLabel : url;
                        youtubeEmbeds.Add(new YouTubeEmbedReference(url, title, link.Span.Start, link.Span.End));
                    }
                }
                else
                {
                    var label = !string.IsNullOrWhiteSpace(altOrLabel) ? altOrLabel : url;
                    var byteStart = Encoding.UTF8.GetByteCount(plainTextBuilder.ToString());
                    plainTextBuilder.Append(label);
                    var byteEnd = Encoding.UTF8.GetByteCount(plainTextBuilder.ToString());

                    textLinks.Add(new LinkReference(label, url, byteStart, byteEnd));
                }
                break;

            case EmphasisInline emphasis:
                unsupportedDetails.Add(emphasis.DelimiterCount >= 2 ? "Bold formatting" : "Italic formatting");
                if (emphasis.FirstChild != null)
                {
                    foreach (var child in emphasis)
                    {
                        ProcessInline(child, plainTextBuilder, mediaEmbeds, youtubeEmbeds, textLinks, unsupportedDetails);
                    }
                }
                break;

            case CodeInline code:
                plainTextBuilder.Append(code.Content);
                break;

            case LiteralInline literal:
                plainTextBuilder.Append(literal.Content.AsSpan());
                break;

            case LineBreakInline:
                plainTextBuilder.Append('\n');
                break;

            case AutolinkInline autolink:
                plainTextBuilder.Append(autolink.Url);
                break;

            case ContainerInline container:
                ProcessInlines(container, plainTextBuilder, mediaEmbeds, youtubeEmbeds, textLinks, unsupportedDetails);
                break;
        }
    }

    public static string ExtractInlineText(Inline? inline)
    {
        if (inline == null) return string.Empty;
        var sb = new StringBuilder();
        Walk(inline, sb);
        return sb.ToString();

        static void Walk(Inline node, StringBuilder builder)
        {
            switch (node)
            {
                case LiteralInline lit:
                    builder.Append(lit.Content.AsSpan());
                    break;
                case CodeInline code:
                    builder.Append(code.Content);
                    break;
                case LineBreakInline:
                    builder.Append('\n');
                    break;
                case ContainerInline container:
                    foreach (var child in container)
                    {
                        Walk(child, builder);
                    }
                    break;
            }
        }
    }

    public static bool IsYouTubeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("youtube.com/shorts/", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("youtube.com/live/", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("youtube.com/embed/", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool OverlapsAny(HashSet<(int Start, int End)> usedRanges, int byteStart, int byteEnd)
    {
        return usedRanges.Any(range => !(byteEnd <= range.Start || byteStart >= range.End));
    }
}
