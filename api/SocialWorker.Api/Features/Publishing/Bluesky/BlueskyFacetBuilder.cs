using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Publishing.Bluesky;

public sealed class BlueskyFacetBuilder
{
    public BlueskyTextFacets Build(string markdownText)
    {
        var doc = ThreadMarkdownDocument.Parse(markdownText);
        var segment = doc.Segments.FirstOrDefault() ?? ThreadSegmentDocument.CreateFromBlocks(System.Array.Empty<Markdig.Syntax.Block>(), string.Empty);
        return Build(segment);
    }

    public BlueskyTextFacets Build(ThreadSegmentDocument segment)
    {
        var facets = new List<BlueskyFacet>();
        var usedRanges = new HashSet<(int Start, int End)>();

        foreach (var link in segment.TextLinks)
        {
            if (!OverlapsAny(usedRanges, link.PlainTextByteStart, link.PlainTextByteEnd))
            {
                facets.Add(new BlueskyFacet
                {
                    Index = new BlueskyFacetIndex { ByteStart = link.PlainTextByteStart, ByteEnd = link.PlainTextByteEnd },
                    Features = new List<BlueskyFacetFeature>
                    {
                        new BlueskyFacetFeature { Type = "app.bsky.richtext.facet#link", Uri = link.Url }
                    }
                });
                usedRanges.Add((link.PlainTextByteStart, link.PlainTextByteEnd));
            }
        }

        foreach (var tag in segment.Hashtags)
        {
            if (!OverlapsAny(usedRanges, tag.PlainTextByteStart, tag.PlainTextByteEnd))
            {
                facets.Add(new BlueskyFacet
                {
                    Index = new BlueskyFacetIndex { ByteStart = tag.PlainTextByteStart, ByteEnd = tag.PlainTextByteEnd },
                    Features = new List<BlueskyFacetFeature>
                    {
                        new BlueskyFacetFeature { Type = "app.bsky.richtext.facet#tag", Tag = tag.Tag }
                    }
                });
                usedRanges.Add((tag.PlainTextByteStart, tag.PlainTextByteEnd));
            }
        }

        foreach (var bare in segment.BareUrls)
        {
            if (!OverlapsAny(usedRanges, bare.PlainTextByteStart, bare.PlainTextByteEnd))
            {
                facets.Add(new BlueskyFacet
                {
                    Index = new BlueskyFacetIndex { ByteStart = bare.PlainTextByteStart, ByteEnd = bare.PlainTextByteEnd },
                    Features = new List<BlueskyFacetFeature>
                    {
                        new BlueskyFacetFeature { Type = "app.bsky.richtext.facet#link", Uri = bare.Url }
                    }
                });
                usedRanges.Add((bare.PlainTextByteStart, bare.PlainTextByteEnd));
            }
        }

        return new BlueskyTextFacets(segment.PlainText, facets);
    }

    private static bool OverlapsAny(HashSet<(int Start, int End)> usedRanges, int byteStart, int byteEnd)
    {
        return usedRanges.Any(range => !(byteEnd <= range.Start || byteStart >= range.End));
    }
}

