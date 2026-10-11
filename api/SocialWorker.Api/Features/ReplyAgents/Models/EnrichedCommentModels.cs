using System.Collections.Generic;

namespace SocialWorker.Api.Features.ReplyAgents.Models;

public sealed record ParsedLinkContext(
    string Url,
    string? Title,
    string? Snippet,
    bool Success
);

public sealed record ParsedImageContext(
    string ImageUrl,
    string? AltText,
    string? VisualDescription
);

public sealed record EnrichedCommentContext(
    PlatformCommentItem Comment,
    IReadOnlyList<ParsedLinkContext> Links,
    IReadOnlyList<ParsedImageContext> Images,
    string FormattedContextForLlm
);
