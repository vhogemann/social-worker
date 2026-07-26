using System;
using FluentValidation;

namespace SocialWorker.Api.Features.Sources.Validators;

public sealed class ImportSourceFromUrlRequestValidator : AbstractValidator<ImportSourceFromUrlRequest>
{
    public ImportSourceFromUrlRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("A valid source URL is required.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("A valid absolute HTTP or HTTPS URL is required.");
    }
}
