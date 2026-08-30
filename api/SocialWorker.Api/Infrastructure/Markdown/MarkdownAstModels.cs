using System;

namespace SocialWorker.Api.Infrastructure.Markdown;

public sealed record MediaEmbedReference(Guid MediaId, string AltText, int SourceSpanStart, int SourceSpanEnd);

public sealed record YouTubeEmbedReference(string Url, string Title, int SourceSpanStart, int SourceSpanEnd);

public sealed record LinkReference(string Text, string Url, int PlainTextByteStart, int PlainTextByteEnd);

public sealed record HashtagReference(string Tag, int PlainTextByteStart, int PlainTextByteEnd);

public sealed record BareUrlReference(string Url, int PlainTextByteStart, int PlainTextByteEnd);
