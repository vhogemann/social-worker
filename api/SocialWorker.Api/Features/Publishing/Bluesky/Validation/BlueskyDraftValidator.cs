using System;
using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.Validation;

namespace SocialWorker.Api.Features.Publishing.Bluesky.Validation;

public sealed class BlueskyDraftValidator
{
    private readonly BlueskyContentValidator _contentValidator;
    private readonly IReadOnlyList<IValidateDraftRule<BlueskySegmentValidation>> _rules;

    public BlueskyDraftValidator(
        BlueskyContentValidator contentValidator,
        IEnumerable<IValidateDraftRule<BlueskySegmentValidation>> rules)
    {
        _contentValidator = contentValidator;
        _rules = rules.ToList();
    }

    public ValidateDraftResult Validate(string content, IReadOnlyList<MediaAsset> mediaAssets)
    {
        var segments = _contentValidator.Analyze(content);
        bool hasErrors = false;
        bool hasWarnings = false;
        var posts = new List<ValidateDraftPostResult>(segments.Count);

        for (int i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            var context = new ValidateDraftRuleContext<BlueskySegmentValidation>(i + 1, segment, mediaAssets);
            var issues = new List<ValidateDraftIssue>();

            foreach (var rule in _rules)
            {
                issues.AddRange(rule.Evaluate(context));
            }

            hasErrors |= issues.Any(issue => issue.Severity == ValidateDraftSeverity.Error);
            hasWarnings |= issues.Any(issue => issue.Severity == ValidateDraftSeverity.Warning);

            posts.Add(new ValidateDraftPostResult(
                context.PostNumber,
                segment.CharacterCount,
                segment.ImageCount,
                segment.HasYouTube,
                issues));
        }

        var overallStatus = hasErrors
            ? ValidateDraftOverallStatus.Failed
            : hasWarnings
                ? ValidateDraftOverallStatus.Warnings
                : ValidateDraftOverallStatus.Valid;

        return new ValidateDraftResult(posts, overallStatus);
    }

    public static ValidateDraftResult CreateFailureResult(string message)
    {
        return new ValidateDraftResult(
            new[]
            {
                new ValidateDraftPostResult(
                    1,
                    0,
                    0,
                    false,
                    new[] { new ValidateDraftIssue(ValidateDraftSeverity.Error, message) })
            },
            ValidateDraftOverallStatus.Failed);
    }
}