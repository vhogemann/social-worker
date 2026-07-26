using System;
using FluentValidation;

namespace SocialWorker.Api.Features.Media.Validators;

public sealed class ImportMediaFromUrlRequestValidator : AbstractValidator<ImportMediaFromUrlRequest>
{
    public ImportMediaFromUrlRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("A valid image URL is required.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("A valid absolute HTTP or HTTPS image URL is required.");
    }
}
