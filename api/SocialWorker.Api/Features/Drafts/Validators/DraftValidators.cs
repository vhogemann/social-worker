using System;
using FluentValidation;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Features.Drafts.Validators;

public sealed class CreateDraftRequestValidator : AbstractValidator<CreateDraftRequest>
{
    public CreateDraftRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.TargetPlatform)
            .Must(p => Enum.TryParse<SocialPlatform>(p, true, out _))
            .WithMessage("Invalid target platform.")
            .When(x => !string.IsNullOrWhiteSpace(x.TargetPlatform));
    }
}

public sealed class CreateReplyDraftFromUrlRequestValidator : AbstractValidator<CreateReplyDraftFromUrlRequest>
{
    public CreateReplyDraftFromUrlRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("A valid Bluesky post URL is required.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("A valid absolute HTTP or HTTPS Bluesky post URL is required.");
    }
}

public sealed class UpdateDraftRequestValidator : AbstractValidator<UpdateDraftRequest>
{
    public UpdateDraftRequestValidator()
    {
        RuleFor(x => x.Title)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.Title));

        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse<DraftStatus>(s, true, out _))
            .WithMessage("Invalid draft status.")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));
    }
}

public sealed class CreatePlatformThreadRequestValidator : AbstractValidator<CreatePlatformThreadRequest>
{
    public CreatePlatformThreadRequestValidator()
    {
        RuleFor(x => x.Platform)
            .NotEmpty().WithMessage("Platform is required.")
            .Must(p => Enum.TryParse<SocialPlatform>(p, true, out _))
            .WithMessage("Invalid platform name.");
    }
}

public sealed class UpdateDraftBlueskyReplyTargetFromUrlRequestValidator : AbstractValidator<UpdateDraftBlueskyReplyTargetFromUrlRequest>
{
    public UpdateDraftBlueskyReplyTargetFromUrlRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("A valid Bluesky post URL is required.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("A valid absolute HTTP or HTTPS Bluesky post URL is required.");
    }
}
