using System;
using System.Collections.Generic;
using FluentValidation;

namespace SocialWorker.Api.Features.Feeds.Validators;

public static class FeedValidationConstants
{
    public static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
        "instagram",
        "x",
        "twitter",
        "telegram",
        "reddit",
        "tiktok",
        "bluesky"
    };

    public static bool IsValidFeedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return AllowedSchemes.Contains(uri.Scheme);
    }
}

public sealed class DiscoverFeedRequestValidator : AbstractValidator<DiscoverFeedRequest>
{
    public DiscoverFeedRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("A valid feed URL is required.")
            .Must(FeedValidationConstants.IsValidFeedUrl)
            .WithMessage("A valid absolute HTTP, HTTPS, or supported platform URL (instagram://, x://, telegram://, reddit://, tiktok://, bluesky://) is required.");
    }
}

public sealed class CreateFeedSubscriptionRequestValidator : AbstractValidator<CreateFeedSubscriptionRequest>
{
    public CreateFeedSubscriptionRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(300).WithMessage("Title cannot exceed 300 characters.");

        RuleFor(x => x.FeedUrl)
            .NotEmpty().WithMessage("FeedUrl is required.")
            .Must(FeedValidationConstants.IsValidFeedUrl)
            .WithMessage("A valid absolute HTTP, HTTPS, or supported platform URL (instagram://, x://, telegram://, reddit://, tiktok://, bluesky://) is required for FeedUrl.");
    }
}
