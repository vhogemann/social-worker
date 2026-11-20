using System;
using System.Diagnostics.CodeAnalysis;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Infrastructure.Platforms;

public static class SocialPlatformParser
{
    public static bool TryParse(string? value, out SocialPlatform platform)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            platform = default;
            return false;
        }

        var trimmed = value.Trim();

        if (string.Equals(trimmed, "threads", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "meta threads", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "metathreads", StringComparison.OrdinalIgnoreCase))
        {
            platform = SocialPlatform.MetaThreads;
            return true;
        }

        if (string.Equals(trimmed, "x", StringComparison.OrdinalIgnoreCase))
        {
            platform = SocialPlatform.Twitter;
            return true;
        }

        if (string.Equals(trimmed, "githubblog", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "github-blog", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "github_blog", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "github blog", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "github", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "jekyll", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "blog", StringComparison.OrdinalIgnoreCase))
        {
            platform = SocialPlatform.GitHubBlog;
            return true;
        }

        return Enum.TryParse(trimmed, ignoreCase: true, out platform);
    }

    public static SocialPlatform? ParseOrNull(string? value)
    {
        return TryParse(value, out var platform) ? platform : null;
    }

    public static string ToPlatformString(SocialPlatform platform)
    {
        return platform.ToString();
    }

    public static string GetDisplayName(SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.MetaThreads => "Threads",
            SocialPlatform.GitHubBlog => "GitHub Blog",
            _ => platform.ToString()
        };
    }
}
