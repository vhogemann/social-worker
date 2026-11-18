using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Markdown;
using SocialWorker.Api.Infrastructure.Security;
using SocialWorker.Api.Infrastructure.Services;

namespace SocialWorker.Api.Features.Publishing.Instagram;

public class InstagramPublisher : IPublisher
{
    private readonly InstagramApiClient _apiClient;
    private readonly AppDbContext _db;
    private readonly string _encryptionKey;
    private readonly string? _publicBaseUrl;
    private readonly IAppUrlResolver? _urlResolver;
    private readonly ILogger<InstagramPublisher> _logger;

    public string Platform => "Instagram";

    public InstagramPublisher(
        IConfiguration config,
        AppDbContext db,
        InstagramApiClient apiClient,
        ILogger<InstagramPublisher> logger,
        IAppUrlResolver? urlResolver = null)
    {
        _db = db;
        _apiClient = apiClient;
        _logger = logger;
        _encryptionKey = config["Auth:DbEncryptionKey"] ?? "";
        _urlResolver = urlResolver ?? new AppUrlResolver(config);
        _publicBaseUrl = config["App:PublicBaseUrl"] ?? config["App:BaseUrl"] ?? config["APP_BASE_URL"];
    }

    public async Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_encryptionKey))
        {
            return PublishResult.Failed("Server encryption key not configured.");
        }

        string accessToken;
        try
        {
            accessToken = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
        }
        catch (Exception ex)
        {
            return PublishResult.Failed($"Failed to decrypt credentials: {ex.Message}");
        }

        var igUserId = account.ExternalId;
        if (string.IsNullOrWhiteSpace(igUserId))
        {
            return PublishResult.Failed("Instagram account is missing user ID (ExternalId). Please reconnect the account.");
        }

        var existingPosts = await _db.Posts
            .Where(p => p.PlatformThreadId == thread.Id)
            .OrderBy(p => p.SegmentIndex)
            .ToListAsync(ct);

        if (existingPosts.Count > 0)
        {
            return PublishResult.Succeeded(existingPosts.Select(p => new PublishedPost
            {
                SegmentIndex = p.SegmentIndex,
                RemoteId = p.RemoteId ?? "",
                RemoteCid = p.RemoteCid,
                PublishedText = p.PublishedText,
                Url = p.Url ?? ""
            }));
        }

        var doc = ThreadMarkdownDocument.Parse(thread.Content ?? "");
        if (doc.Segments.Count == 0)
        {
            return PublishResult.Failed("Thread is empty.");
        }

        for (int i = 1; i < doc.Segments.Count; i++)
        {
            var extraSeg = doc.Segments[i];
            if (!string.IsNullOrWhiteSpace(extraSeg.PlainText) || extraSeg.YouTubeEmbed != null)
            {
                return PublishResult.Failed($"Instagram carousel captions come only from segment 1; segment {i + 1} must not contain caption text.");
            }
        }

        var firstSegment = doc.Segments[0];
        var caption = firstSegment.PlainText;
        if (firstSegment.YouTubeEmbed != null)
        {
            caption = string.IsNullOrWhiteSpace(caption)
                ? firstSegment.YouTubeEmbed.Url
                : $"{caption}\n\n{firstSegment.YouTubeEmbed.Url}";
        }

        var mediaEmbeds = doc.Segments.SelectMany(s => s.MediaEmbeds).ToList();
        if (mediaEmbeds.Count == 0)
        {
            return PublishResult.Failed("Instagram posts require at least one image attachment.");
        }

        if (mediaEmbeds.Count > 10)
        {
            return PublishResult.Failed($"Instagram carousels support at most 10 images. Found {mediaEmbeds.Count}.");
        }

        var resolvedMedia = new List<(string ImageUrl, string? AltText)>(mediaEmbeds.Count);
        foreach (var mediaEmbed in mediaEmbeds)
        {
            var asset = await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == mediaEmbed.MediaId, ct);
            if (asset == null)
            {
                return PublishResult.Failed($"Media asset {mediaEmbed.MediaId} was not found.");
            }

            if (!string.IsNullOrWhiteSpace(asset.MimeType) &&
                !asset.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return PublishResult.Failed($"Instagram publishing currently supports images only; '{asset.FileName}' has MIME type '{asset.MimeType}'.");
            }

            var imageUrl = ResolveImageUrl(asset, mediaEmbed.MediaId);
            var altText = !string.IsNullOrWhiteSpace(mediaEmbed.AltText) ? mediaEmbed.AltText : asset.AltText;
            resolvedMedia.Add((imageUrl, altText));
        }

        try
        {
            string containerId;

            if (resolvedMedia.Count == 1)
            {
                var item = resolvedMedia[0];
                var container = await _apiClient.CreateImageContainerAsync(
                    igUserId,
                    accessToken,
                    item.ImageUrl,
                    caption: caption,
                    altText: item.AltText,
                    isCarouselItem: false,
                    ct: ct);
                containerId = container.Id;
            }
            else
            {
                var childIds = new List<string>(resolvedMedia.Count);
                foreach (var item in resolvedMedia)
                {
                    var child = await _apiClient.CreateImageContainerAsync(
                        igUserId,
                        accessToken,
                        item.ImageUrl,
                        caption: null,
                        altText: item.AltText,
                        isCarouselItem: true,
                        ct: ct);

                    await _apiClient.WaitForContainerReadyAsync(child.Id, accessToken, ct: ct);
                    childIds.Add(child.Id);
                }

                var carouselContainer = await _apiClient.CreateCarouselContainerAsync(
                    igUserId,
                    accessToken,
                    childIds,
                    caption: caption,
                    ct: ct);
                containerId = carouselContainer.Id;
            }

            await _apiClient.WaitForContainerReadyAsync(containerId, accessToken, ct: ct);

            var published = await _apiClient.PublishContainerAsync(igUserId, accessToken, containerId, ct);
            var permalinkInfo = await _apiClient.GetMediaPermalinkAsync(published.Id, accessToken, ct);
            var postUrl = BuildPostUrl(account.Handle, permalinkInfo);

            _logger.LogInformation("Successfully published to Instagram. MediaId={MediaId}", published.Id);

            return PublishResult.Succeeded(new[]
            {
                new PublishedPost
                {
                    SegmentIndex = 0,
                    RemoteId = published.Id,
                    PublishedText = caption,
                    Url = postUrl
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish to Instagram: {Message}", ex.Message);
            return PublishResult.Failed(ex.Message);
        }
    }

    private string ResolveImageUrl(MediaAsset asset, Guid mediaId)
    {
        if (asset.FilePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            asset.FilePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return asset.FilePath;
        }

        if (_urlResolver != null)
        {
            return _urlResolver.GetMediaUrl(mediaId);
        }

        if (!string.IsNullOrWhiteSpace(_publicBaseUrl))
        {
            var baseTrim = _publicBaseUrl.TrimEnd('/');
            return baseTrim.EndsWith("/api", StringComparison.OrdinalIgnoreCase)
                ? $"{baseTrim}/media/{mediaId}"
                : $"{baseTrim}/api/media/{mediaId}";
        }

        throw new InvalidOperationException($"Unable to resolve public URL for media asset {mediaId}. Set App:PublicBaseUrl or App:BaseUrl in configuration.");
    }

    private static string BuildPostUrl(string handle, InstagramMediaPermalinkResponse? permalinkInfo)
    {
        if (!string.IsNullOrWhiteSpace(permalinkInfo?.Permalink))
        {
            return permalinkInfo.Permalink;
        }

        if (!string.IsNullOrWhiteSpace(permalinkInfo?.Shortcode))
        {
            return $"https://www.instagram.com/p/{permalinkInfo.Shortcode}/";
        }

        var cleanHandle = handle.TrimStart('@');
        return !string.IsNullOrWhiteSpace(cleanHandle)
            ? $"https://www.instagram.com/{cleanHandle}/"
            : "https://www.instagram.com/";
    }
}
