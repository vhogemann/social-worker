using System;
using FluentValidation;

namespace SocialWorker.Api.Features.Feeds.Validators;

public sealed class DiscoverFeedRequestValidator : AbstractValidator<DiscoverFeedRequest>
{
    public DiscoverFeedRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("A valid feed URL is required.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("A valid absolute HTTP or HTTPS URL is required.");
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
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("A valid absolute HTTP or HTTPS URL is required for FeedUrl.");
    }
}
