using System;
using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Infrastructure;

namespace SocialWorker.Api.Features.Publishing.Validation;

public sealed class PlatformPolicyDraftValidator : IDraftValidator
{
    private readonly SocialPlatform _platform;
    private readonly PlatformContentPolicy _policy;

    public PlatformPolicyDraftValidator(SocialPlatform platform, PlatformContentPolicy policy)
    {
        _platform = platform;
        _policy = policy;
    }

    public ValidateDraftResult Validate(string content, IReadOnlyList<MediaAsset> mediaAssets)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ValidateDraftResult(
                new[]
                {
                    new ValidateDraftPostResult(
                        1,
                        0,
                        0,
                        false,
                        new[] { new ValidateDraftIssue(ValidateDraftSeverity.Error, "Draft content cannot be empty.") })
                },
                ValidateDraftOverallStatus.Failed);
        }

        var policyResult = _policy.Evaluate(_platform, content, normalizeFormatting: false);
        var rawSegments = DraftSegmentService.SplitMarkdownIntoSegments(content);

        var posts = new List<ValidateDraftPostResult>();
        bool hasErrors = false;
        bool hasWarnings = false;

        for (int i = 0; i < rawSegments.Count; i++)
        {
            var segment = rawSegments[i];
            var postNumber = i + 1;
            var issues = new List<ValidateDraftIssue>();

            var cleanedText = SharedPatterns.StripMediaMarkdown(segment).Trim();
            var imageCount = SharedPatterns.CountMediaReferences(segment);
            var hasYouTube = segment.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
                             segment.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);

            // If there's only 1 segment, associate all policy errors/warnings with this post
            // If multi-segment, associate general errors
            if (rawSegments.Count == 1)
            {
                foreach (var err in policyResult.Errors)
                {
                    issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Error, err));
                }
                foreach (var warn in policyResult.Warnings)
                {
                    issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Warning, warn));
                }
            }
            else
            {
                // Multi-segment: evaluate each segment individually with policy
                var segmentPolicyResult = _policy.Evaluate(_platform, segment, normalizeFormatting: false);
                foreach (var err in segmentPolicyResult.Errors)
                {
                    issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Error, err));
                }
                foreach (var warn in segmentPolicyResult.Warnings)
                {
                    issues.Add(new ValidateDraftIssue(ValidateDraftSeverity.Warning, warn));
                }
            }

            hasErrors |= issues.Any(issue => issue.Severity == ValidateDraftSeverity.Error);
            hasWarnings |= issues.Any(issue => issue.Severity == ValidateDraftSeverity.Warning);

            posts.Add(new ValidateDraftPostResult(
                postNumber,
                cleanedText.Length,
                imageCount,
                hasYouTube,
                issues));
        }

        var overallStatus = hasErrors
            ? ValidateDraftOverallStatus.Failed
            : hasWarnings
                ? ValidateDraftOverallStatus.Warnings
                : ValidateDraftOverallStatus.Valid;

        return new ValidateDraftResult(posts, overallStatus);
    }
}
