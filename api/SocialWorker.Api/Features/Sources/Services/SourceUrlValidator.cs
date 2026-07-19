using System;

namespace SocialWorker.Api.Features.Sources;

public sealed class SourceUrlValidator : ISourceUrlValidator
{
    public void EnsureAbsoluteHttpUrl(string reference, string paramName = "reference")
    {
        if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Source URLs must be absolute HTTP or HTTPS URLs.", paramName);
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Source URLs must use http:// or https://.", paramName);
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("Source URLs must include a valid host.", paramName);
        }
    }
}
