using System;
using System.Text.RegularExpressions;

namespace SocialWorker.Api.Features.Sources.Instagram;

public static class InstagramUrlHelper
{
    private static readonly Regex InstagramProfileRegex = new(
        @"^(?:https?:\/\/(?:www\.)?instagram\.com\/|instagram:\/\/|@)([a-zA-Z0-9_\.]+)\/?(?:\?.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsInstagramUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var trimmed = url.Trim();
        if (trimmed.StartsWith("instagram://", StringComparison.OrdinalIgnoreCase)) return true;
        if (trimmed.Contains("instagram.com", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static string? ExtractUsername(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();

        var match = InstagramProfileRegex.Match(trimmed);
        if (match.Success)
        {
            var username = match.Groups[1].Value.Trim().Trim('@', '/');
            if (!string.IsNullOrWhiteSpace(username) &&
                !username.Equals("p", StringComparison.OrdinalIgnoreCase) &&
                !username.Equals("reel", StringComparison.OrdinalIgnoreCase) &&
                !username.Equals("stories", StringComparison.OrdinalIgnoreCase) &&
                !username.Equals("explore", StringComparison.OrdinalIgnoreCase))
            {
                return username;
            }
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            if (uri.Host.Contains("instagram.com", StringComparison.OrdinalIgnoreCase))
            {
                var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 1)
                {
                    var first = segments[0].Trim('@');
                    if (!first.Equals("p", StringComparison.OrdinalIgnoreCase) &&
                        !first.Equals("reel", StringComparison.OrdinalIgnoreCase) &&
                        !first.Equals("stories", StringComparison.OrdinalIgnoreCase) &&
                        !first.Equals("explore", StringComparison.OrdinalIgnoreCase))
                    {
                        return first;
                    }
                }
            }
        }

        return null;
    }

    public static string NormalizeFeedUrl(string username)
    {
        var clean = username.Trim().Trim('@', '/');
        return $"instagram://{clean}";
    }

    public static string GetWebUrl(string username)
    {
        var clean = username.Trim().Trim('@', '/');
        return $"https://www.instagram.com/{clean}";
    }
}
