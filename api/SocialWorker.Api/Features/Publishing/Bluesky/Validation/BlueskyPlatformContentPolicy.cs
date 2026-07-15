using System;
using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Publishing.Bluesky.Validation;

public sealed class BlueskyPlatformContentPolicy : IPlatformContentPolicy
{
    private readonly BlueskyContentValidator _contentValidator;
    private readonly BlueskyMaxCharactersRule _maxCharactersRule;
    private readonly BlueskyMarkdownStyleWarningRule _markdownStyleWarningRule;

    public BlueskyPlatformContentPolicy()
        : this(new BlueskyContentValidator(), new BlueskyMaxCharactersRule(), new BlueskyMarkdownStyleWarningRule())
    {
    }

    public BlueskyPlatformContentPolicy(
        BlueskyContentValidator contentValidator,
        BlueskyMaxCharactersRule maxCharactersRule,
        BlueskyMarkdownStyleWarningRule markdownStyleWarningRule)
    {
        _contentValidator = contentValidator;
        _maxCharactersRule = maxCharactersRule;
        _markdownStyleWarningRule = markdownStyleWarningRule;
    }

    public SocialPlatform Platform => SocialPlatform.Bluesky;

    public string GetAdaptationRules()
    {
        return """
Bluesky rules:
- 300 characters per post maximum
- Keep copy concise and direct
- Prefer thread segmentation with --- on separate lines
""";
    }

    public PlatformContentPolicyResult Evaluate(string content, bool normalizeFormatting)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var normalized = normalizeFormatting ? Normalize(content) : content;

        if (_markdownStyleWarningRule.Evaluate(new ValidateDraftRuleContext<BlueskySegmentValidation>(1, new BlueskySegmentValidation(content, content, content.Length, 0, false, false), Array.Empty<MediaAsset>())).Any())
        {
            warnings.Add("Markdown styling detected. Most platforms will show these markers as plain text.");
        }

        if (normalizeFormatting && !string.Equals(content, normalized, StringComparison.Ordinal))
        {
            warnings.Add("Unsupported markdown styling was removed during normalization.");
        }

        var segments = _contentValidator.Analyze(normalized);
        for (var i = 0; i < segments.Count; i++)
        {
            var context = new ValidateDraftRuleContext<BlueskySegmentValidation>(i + 1, segments[i], Array.Empty<MediaAsset>());
            foreach (var issue in _maxCharactersRule.Evaluate(context))
            {
                if (issue.Severity == ValidateDraftSeverity.Error)
                {
                    errors.Add($"Post {context.PostNumber} exceeds 300 characters for Bluesky ({context.Segment.CharacterCount}).");
                }
            }
        }

        return new PlatformContentPolicyResult(errors.Count == 0, normalized, errors, warnings);
    }

    private static string Normalize(string content)
    {
        var normalized = content;
        normalized = PlatformContentPolicyBase.BoldAsteriskRegex.Replace(normalized, "$1");
        normalized = PlatformContentPolicyBase.BoldUnderscoreRegex.Replace(normalized, "$1");
        normalized = PlatformContentPolicyBase.ItalicAsteriskRegex.Replace(normalized, "$1");
        normalized = PlatformContentPolicyBase.HeadingRegex.Replace(normalized, "");
        return normalized;
    }
}