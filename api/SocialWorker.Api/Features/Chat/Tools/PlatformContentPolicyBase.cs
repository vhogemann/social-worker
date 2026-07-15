using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Drafts;

namespace SocialWorker.Api.Features.Chat.Tools;

public abstract class PlatformContentPolicyBase : IPlatformContentPolicy
{
    public static readonly Regex BoldAsteriskRegex = new(@"\*\*([^*]+)\*\*", RegexOptions.Compiled);
    public static readonly Regex BoldUnderscoreRegex = new(@"__([^_]+)__", RegexOptions.Compiled);
    public static readonly Regex ItalicAsteriskRegex = new(@"(?<!\*)\*([^*\n]+)\*(?!\*)", RegexOptions.Compiled);
    public static readonly Regex HeadingRegex = new(@"(?m)^\s{0,3}#{1,6}\s+", RegexOptions.Compiled);
    public static readonly Regex UnsupportedMarkdownRegex = new(@"\*\*[^*]+\*\*|__[^_]+__|(?<!\*)\*[^*\n]+\*(?!\*)|(?m)^\s{0,3}#{1,6}\s+", RegexOptions.Compiled);

    public abstract SocialPlatform Platform { get; }

    public abstract string GetAdaptationRules();

    public virtual PlatformContentPolicyResult Evaluate(string content, bool normalizeFormatting)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var normalized = normalizeFormatting ? Normalize(content) : content;

        if (UnsupportedMarkdownRegex.IsMatch(content))
        {
            warnings.Add("Markdown styling detected. Most platforms will show these markers as plain text.");
        }

        if (normalizeFormatting && !string.Equals(content, normalized, StringComparison.Ordinal))
        {
            warnings.Add("Unsupported markdown styling was removed during normalization.");
        }

        EvaluateSegments(normalized, errors, warnings);

        return new PlatformContentPolicyResult(errors.Count == 0, normalized, errors, warnings);
    }

    protected abstract void EvaluateSegments(string normalizedContent, List<string> errors, List<string> warnings);

    protected static string Normalize(string content)
    {
        var normalized = content;
        normalized = BoldAsteriskRegex.Replace(normalized, "$1");
        normalized = BoldUnderscoreRegex.Replace(normalized, "$1");
        normalized = ItalicAsteriskRegex.Replace(normalized, "$1");
        normalized = HeadingRegex.Replace(normalized, "");
        return normalized;
    }

    protected static string[] SplitSegments(string content)
    {
        return DraftSegmentService.SplitMarkdownIntoSegments(content).ToArray();
    }
}