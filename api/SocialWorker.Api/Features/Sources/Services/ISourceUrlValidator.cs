using System;

namespace SocialWorker.Api.Features.Sources;

public interface ISourceUrlValidator
{
    void EnsureAbsoluteHttpUrl(string reference, string paramName = "reference");
}
