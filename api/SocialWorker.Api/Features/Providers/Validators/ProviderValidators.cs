using System;
using FluentValidation;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Providers.Validators;

public sealed class CreateProviderRequestValidator : AbstractValidator<ProviderModels.CreateProviderRequest>
{
    public CreateProviderRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Provider name is required.")
            .MaximumLength(200);

        RuleFor(x => x.ProviderType)
            .NotEmpty().WithMessage("ProviderType is required.")
            .Must(t => string.Equals(t, "OpenRouter", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(t, "Ollama", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(t, "OpenAI", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Invalid ProviderType. Must be OpenRouter, Ollama, or OpenAI.");

        RuleFor(x => x.BaseUrl)
            .NotEmpty().WithMessage("BaseUrl is required.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("A valid absolute HTTP or HTTPS BaseUrl is required.");

        RuleFor(x => x.Model)
            .NotEmpty().WithMessage("Model is required.");
    }
}

public sealed class UpdateProviderRequestValidator : AbstractValidator<ProviderModels.UpdateProviderRequest>
{
    public UpdateProviderRequestValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.Name));

        RuleFor(x => x.ProviderType)
            .Must(t => string.Equals(t, "OpenRouter", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(t, "Ollama", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(t, "OpenAI", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Invalid ProviderType. Must be OpenRouter, Ollama, or OpenAI.")
            .When(x => !string.IsNullOrWhiteSpace(x.ProviderType));

        RuleFor(x => x.BaseUrl)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                         (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("A valid absolute HTTP or HTTPS BaseUrl is required.")
            .When(x => !string.IsNullOrWhiteSpace(x.BaseUrl));
    }
}
