using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace SocialWorker.Api.Infrastructure.Services;

public interface IAppUrlResolver
{
    string GetWebBaseUrl();
    string GetApiBaseUrl();
    string GetMediaUrl(Guid mediaId);
    string GetDraftWebUrl(Guid draftId);
    string GetDraftPreviewHtmlUrl(Guid draftId, string? platform = null);
}

public sealed class AppUrlResolver : IAppUrlResolver
{
    private readonly IConfiguration _config;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public AppUrlResolver(IConfiguration config, IHttpContextAccessor? httpContextAccessor = null)
    {
        _config = config;
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetRawBaseUrl()
    {
        var configured = _config["App:BaseUrl"]
            ?? _config["APP_BASE_URL"]
            ?? _config["BASE_URL"]
            ?? _config["App:PublicBaseUrl"]
            ?? _config["APP__PUBLICBASEURL"]
            ?? _config["MetaThreads:PublicBaseUrl"]
            ?? _config["METATHREADS__PUBLICBASEURL"];

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim().TrimEnd('/');
        }

        var ctx = _httpContextAccessor?.HttpContext;
        if (ctx != null)
        {
            try
            {
                var scheme = ctx.Request.Headers["X-Forwarded-Proto"].ToString();
                if (!string.IsNullOrWhiteSpace(scheme))
                {
                    scheme = scheme.Split(',')[0].Trim();
                }
                else
                {
                    scheme = ctx.Request.Scheme;
                }

                var host = ctx.Request.Headers["X-Forwarded-Host"].ToString();
                if (!string.IsNullOrWhiteSpace(host))
                {
                    host = host.Split(',')[0].Trim();
                }
                else
                {
                    host = ctx.Request.Host.Value ?? "";
                }

                if (!string.IsNullOrEmpty(host) &&
                    !host.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) &&
                    !host.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
                    !host.StartsWith("api:", StringComparison.OrdinalIgnoreCase) &&
                    !host.StartsWith("web:", StringComparison.OrdinalIgnoreCase))
                {
                    scheme = "https";
                }

                var url = $"{scheme}://{host}{ctx.Request.PathBase}".TrimEnd('/');
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }
            catch
            {
            }
        }

        return "http://localhost:8100";
    }

    public string GetWebBaseUrl()
    {
        var raw = GetRawBaseUrl();
        if (raw.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            return raw.Substring(0, raw.Length - 4).TrimEnd('/');
        }
        return raw;
    }

    public string GetApiBaseUrl()
    {
        var raw = GetRawBaseUrl();
        if (raw.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            return raw;
        }
        return $"{raw}/api";
    }

    public string GetMediaUrl(Guid mediaId)
    {
        return $"{GetApiBaseUrl()}/media/{mediaId}";
    }

    public string GetDraftWebUrl(Guid draftId)
    {
        return $"{GetWebBaseUrl()}/draft/{draftId}";
    }

    public string GetDraftPreviewHtmlUrl(Guid draftId, string? platform = null)
    {
        var url = $"{GetApiBaseUrl()}/drafts/{draftId}/preview";
        if (!string.IsNullOrWhiteSpace(platform))
        {
            url += $"?platform={Uri.EscapeDataString(platform)}";
        }
        return url;
    }
}
