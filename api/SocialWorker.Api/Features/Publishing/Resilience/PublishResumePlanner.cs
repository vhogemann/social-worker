using System;
using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Publishing.Resilience;

public sealed record PublishResumePlan(
    bool CanResume,
    int ResumeFromSegmentIndex,
    bool IsFullyPublished,
    string? ErrorMessage,
    IReadOnlyList<Post> ExistingPosts);

public static class PublishResumePlanner
{
    public static PublishResumePlan CreatePlan(
        IReadOnlyList<string> currentSegmentTexts,
        IReadOnlyList<Post> existingPosts,
        string platformName)
    {
        var sortedPosts = existingPosts.OrderBy(p => p.SegmentIndex).ToList();

        if (sortedPosts.Count > currentSegmentTexts.Count)
        {
            return new PublishResumePlan(
                CanResume: false,
                ResumeFromSegmentIndex: 0,
                IsFullyPublished: false,
                ErrorMessage: $"Cannot resume publishing: thread contains fewer segments ({currentSegmentTexts.Count}) than have already been published ({sortedPosts.Count}).",
                ExistingPosts: sortedPosts);
        }

        for (int i = 0; i < sortedPosts.Count; i++)
        {
            var post = sortedPosts[i];
            if (post.SegmentIndex != i)
            {
                return new PublishResumePlan(
                    CanResume: false,
                    ResumeFromSegmentIndex: 0,
                    IsFullyPublished: false,
                    ErrorMessage: $"Cannot resume publishing: gap or mismatch in previously published segments (expected index {i}, found {post.SegmentIndex}).",
                    ExistingPosts: sortedPosts);
            }

            if (!string.IsNullOrEmpty(post.PublishedText))
            {
                var currentText = currentSegmentTexts[i]?.Trim() ?? string.Empty;
                var publishedText = post.PublishedText.Trim();
                if (!string.Equals(currentText, publishedText, StringComparison.Ordinal))
                {
                    return new PublishResumePlan(
                        CanResume: false,
                        ResumeFromSegmentIndex: 0,
                        IsFullyPublished: false,
                        ErrorMessage: $"Cannot resume publishing: segment {i + 1} was previously published with different content and cannot be modified on {platformName}.",
                        ExistingPosts: sortedPosts);
                }
            }
        }

        var isFullyPublished = sortedPosts.Count > 0 && sortedPosts.Count == currentSegmentTexts.Count;
        return new PublishResumePlan(
            CanResume: true,
            ResumeFromSegmentIndex: sortedPosts.Count,
            IsFullyPublished: isFullyPublished,
            ErrorMessage: null,
            ExistingPosts: sortedPosts);
    }
}
