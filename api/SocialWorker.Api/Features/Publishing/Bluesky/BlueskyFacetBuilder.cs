using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SocialWorker.Api.Features.Publishing.Bluesky;

public sealed class BlueskyFacetBuilder
{
    private static readonly Regex HashtagRegex = new(@"#([a-zA-Z][a-zA-Z0-9_]*)", RegexOptions.Compiled);
    private static readonly Regex BareUrlRegex = new(@"https?://[^\s\])>""\n]+", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkRegex = new(@"\[([^\]]+)\]\((https?://[^)]+)\)", RegexOptions.Compiled);

    public BlueskyTextFacets Build(string markdownText)
    {
        var facets = new List<BlueskyFacet>();
        var usedRanges = new HashSet<(int Start, int End)>();
        var plainText = MarkdownLinkRegex.Replace(markdownText, "$1");

        var plainByteCursor = 0;
        var originalCursor = 0;

        foreach (Match match in MarkdownLinkRegex.Matches(markdownText))
        {
            if (match.Index > originalCursor)
            {
                var before = markdownText.Substring(originalCursor, match.Index - originalCursor);
                plainByteCursor += Encoding.UTF8.GetByteCount(before);
            }

            var linkText = match.Groups[1].Value;
            var linkUri = match.Groups[2].Value;
            var byteStart = plainByteCursor;
            var byteEnd = byteStart + Encoding.UTF8.GetByteCount(linkText);

            if (!OverlapsAny(usedRanges, byteStart, byteEnd))
            {
                facets.Add(new BlueskyFacet
                {
                    Index = new BlueskyFacetIndex { ByteStart = byteStart, ByteEnd = byteEnd },
                    Features = new List<BlueskyFacetFeature>
                    {
                        new BlueskyFacetFeature { Type = "app.bsky.richtext.facet#link", Uri = linkUri }
                    }
                });

                usedRanges.Add((byteStart, byteEnd));
            }

            plainByteCursor = byteEnd;
            originalCursor = match.Index + match.Length;
        }

        if (originalCursor < markdownText.Length)
        {
            var tail = markdownText.Substring(originalCursor);
            plainByteCursor += Encoding.UTF8.GetByteCount(tail);
        }

        foreach (Match match in HashtagRegex.Matches(plainText))
        {
            var byteStart = Encoding.UTF8.GetByteCount(plainText, 0, match.Index);
            var byteEnd = byteStart + Encoding.UTF8.GetByteCount(plainText, match.Index, match.Length);

            if (OverlapsAny(usedRanges, byteStart, byteEnd))
            {
                continue;
            }

            facets.Add(new BlueskyFacet
            {
                Index = new BlueskyFacetIndex { ByteStart = byteStart, ByteEnd = byteEnd },
                Features = new List<BlueskyFacetFeature>
                {
                    new BlueskyFacetFeature { Type = "app.bsky.richtext.facet#tag", Tag = match.Groups[1].Value }
                }
            });

            usedRanges.Add((byteStart, byteEnd));
        }

        foreach (Match match in BareUrlRegex.Matches(plainText))
        {
            var byteStart = Encoding.UTF8.GetByteCount(plainText, 0, match.Index);
            var byteEnd = byteStart + Encoding.UTF8.GetByteCount(plainText, match.Index, match.Length);

            if (OverlapsAny(usedRanges, byteStart, byteEnd))
            {
                continue;
            }

            facets.Add(new BlueskyFacet
            {
                Index = new BlueskyFacetIndex { ByteStart = byteStart, ByteEnd = byteEnd },
                Features = new List<BlueskyFacetFeature>
                {
                    new BlueskyFacetFeature { Type = "app.bsky.richtext.facet#link", Uri = match.Value }
                }
            });

            usedRanges.Add((byteStart, byteEnd));
        }

        return new BlueskyTextFacets(plainText, facets);
    }

    private static bool OverlapsAny(HashSet<(int Start, int End)> usedRanges, int byteStart, int byteEnd)
    {
        return usedRanges.Any(range => !(byteEnd <= range.Start || byteStart >= range.End));
    }
}
