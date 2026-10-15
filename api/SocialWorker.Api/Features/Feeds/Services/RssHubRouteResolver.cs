using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Sources.Instagram;

namespace SocialWorker.Api.Features.Feeds.Services;

public sealed record RssHubRouteMatch(
    string Platform,
    string RoutePath,
    string CanonicalFeedUrl,
    string WebUrl
);

public interface IRssHubRouteResolver
{
    string GetBaseUrl();
    bool TryResolve(string rawUrl, [NotNullWhen(true)] out RssHubRouteMatch? match);
}

public sealed class RssHubRouteResolver : IRssHubRouteResolver
{
    private readonly RssHubOptions _options;
    private readonly List<(RssHubRouteRule Rule, Regex Regex)> _compiledRules = [];

    public RssHubRouteResolver(
        IOptions<RssHubOptions>? options = null,
        IOptions<InstagramOptions>? instagramOptions = null)
    {
        _options = options?.Value ?? new RssHubOptions();

        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || _options.BaseUrl == "http://rsshub:1200")
        {
            var legacyBase = instagramOptions?.Value?.RssHubBaseUrl;
            if (!string.IsNullOrWhiteSpace(legacyBase))
            {
                _options.BaseUrl = legacyBase;
            }
        }

        var rules = _options.Routes is { Count: > 0 } ? _options.Routes : RssHubOptions.GetDefaultRoutes();
        foreach (var rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule.UrlPattern)) continue;
            try
            {
                var regex = new Regex(rule.UrlPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                _compiledRules.Add((rule, regex));
            }
            catch
            {
            }
        }
    }

    public string GetBaseUrl() => _options.BaseUrl.TrimEnd('/');

    public bool TryResolve(string rawUrl, [NotNullWhen(true)] out RssHubRouteMatch? match)
    {
        match = null;
        if (string.IsNullOrWhiteSpace(rawUrl)) return false;

        var trimmedUrl = rawUrl.Trim();

        foreach (var (rule, regex) in _compiledRules)
        {
            var regexMatch = regex.Match(trimmedUrl);
            if (!regexMatch.Success) continue;

            var routePath = rule.RouteTemplate;
            var canonicalScheme = rule.CanonicalScheme;
            var webUrl = rule.WebUrlTemplate;

            foreach (var groupName in regex.GetGroupNames())
            {
                if (int.TryParse(groupName, out _)) continue;
                var groupVal = regexMatch.Groups[groupName].Value;
                routePath = routePath.Replace($"{{{groupName}}}", Uri.EscapeDataString(groupVal), StringComparison.OrdinalIgnoreCase);
                canonicalScheme = canonicalScheme.Replace($"{{{groupName}}}", groupVal, StringComparison.OrdinalIgnoreCase);
                webUrl = webUrl.Replace($"{{{groupName}}}", groupVal, StringComparison.OrdinalIgnoreCase);
            }

            match = new RssHubRouteMatch(
                Platform: rule.Platform,
                RoutePath: routePath.StartsWith('/') ? routePath : "/" + routePath,
                CanonicalFeedUrl: canonicalScheme,
                WebUrl: webUrl);
            return true;
        }

        return false;
    }
}
