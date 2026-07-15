using System;
using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Validation;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class BlueskyDraftValidatorTests
{
    private sealed class CustomWarningRule : IValidateDraftRule<BlueskySegmentValidation>
    {
        public IEnumerable<ValidateDraftIssue> Evaluate(ValidateDraftRuleContext<BlueskySegmentValidation> context)
        {
            yield return new ValidateDraftIssue(ValidateDraftSeverity.Warning, $"Custom rule hit on post {context.PostNumber}");
        }
    }

    [Fact]
    public void Validate_Applies_Custom_Rules_From_The_Pipeline()
    {
        var validator = new BlueskyDraftValidator(
            new BlueskyContentValidator(),
            new IValidateDraftRule<BlueskySegmentValidation>[] { new CustomWarningRule() });

        var report = validator.Validate("Hello world", Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Warnings, report.OverallStatus);
        Assert.False(report.HasBlockingErrors);
        Assert.Single(report.Posts);
        Assert.Single(report.Posts[0].Issues);
        Assert.Equal("Custom rule hit on post 1", report.Posts[0].Issues[0].Message);
    }
}