using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Resilience;
using SocialWorker.Api.Infrastructure.Markdown;
using SocialWorker.Api.Infrastructure.Security;
using SocialWorker.Api.Infrastructure.Services;

namespace SocialWorker.Api.Features.Publishing.MetaThreads;

public class MetaThreadsPublisher : IPublisher
{
    private readonly MetaThreadsApiClient _apiClient;
    private readonly MetaThreadsOptions _options;
    private readonly AppDbContext _db;
    private readonly string _encryptionKey;
    private readonly string? _publicBaseUrl;
    private readonly IAppUrlResolver? _urlResolver;
    private readonly ILogger<MetaThreadsPublisher> _logger;

    public string Platform => "MetaThreads";

    public MetaThreadsPublisher(
        IConfiguration config,
        IOptions<MetaThreadsOptions> options,
        AppDbContext db,
        MetaThreadsApiClient apiClient,
        ILogger<MetaThreadsPublisher> logger,
        IAppUrlResolver? urlResolver = null)
    {
        _options = options.Value;
        _db = db;
        _apiClient = apiClient;
        _logger = logger;
        _encryptionKey = config["Auth:DbEncryptionKey"] ?? "";
        _urlResolver = urlResolver ?? new AppUrlResolver(config);
        _publicBaseUrl = _options.PublicBaseUrl ?? config["App:BaseUrl"] ?? config["APP_BASE_URL"];
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

        var userId = account.ExternalId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return PublishResult.Failed("Threads account is missing user ID (ExternalId). Please reconnect the account.");
        }

        var doc = ThreadMarkdownDocument.Parse(thread.Content ?? "");
        if (doc.Segments.Count == 0 || (doc.Segments.Count == 1 && string.IsNullOrWhiteSpace(doc.Segments[0].PlainText) && doc.Segments[0].MediaEmbeds.Count == 0 && doc.Segments[0].YouTubeEmbed == null))
        {
            return PublishResult.Failed("Thread is empty.");
        }

        var existingPosts = await _db.Posts
            .Where(p => p.PlatformThreadId == thread.Id)
            .OrderBy(p => p.SegmentIndex)
            .ToListAsync(ct);

        var segmentTexts = doc.Segments.Select(s =>
        {
            var text = s.PlainText;
            if (s.YouTubeEmbed != null)
            {
                text = string.IsNullOrWhiteSpace(text)
                    ? s.YouTubeEmbed.Url
                    : $"{text}\n\n{s.YouTubeEmbed.Url}";
            }
            return text;
        }).ToList();
        var resumePlan = PublishResumePlanner.CreatePlan(segmentTexts, existingPosts, "MetaThreads");

        if (!resumePlan.CanResume)
        {
            return PublishResult.Failed(resumePlan.ErrorMessage!);
        }

        if (resumePlan.IsFullyPublished)
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

        var publishedPosts = new List<PublishedPost>();
        foreach (var p in resumePlan.ExistingPosts)
        {
            publishedPosts.Add(new PublishedPost
            {
                SegmentIndex = p.SegmentIndex,
                RemoteId = p.RemoteId ?? "",
                RemoteCid = p.RemoteCid,
                PublishedText = p.PublishedText,
                Url = p.Url ?? ""
            });
        }

        string? replyToId = resumePlan.ExistingPosts.LastOrDefault()?.RemoteId;

        for (int i = 0; i < doc.Segments.Count; i++)
        {
            if (i < resumePlan.ResumeFromSegmentIndex)
            {
                continue;
            }

            var segment = doc.Segments[i];
            var postText = segment.PlainText;
            if (segment.YouTubeEmbed != null)
            {
                postText = string.IsNullOrWhiteSpace(postText)
                    ? segment.YouTubeEmbed.Url
                    : $"{postText}\n\n{segment.YouTubeEmbed.Url}";
            }

            if (string.IsNullOrWhiteSpace(postText) && segment.MediaEmbeds.Count == 0)
            {
                continue;
            }

            try
            {
                string containerId;
                var mediaEmbeds = segment.MediaEmbeds;

                if (mediaEmbeds.Count == 0)
                {
                    var container = await _apiClient.CreateTextContainerAsync(
                        userId,
                        accessToken,
                        postText,
                        replyToId,
                        ct);
                    containerId = container.Id;
                }
                else if (mediaEmbeds.Count == 1)
                {
                    var mediaEmbed = mediaEmbeds[0];
                    var asset = await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == mediaEmbed.MediaId, ct);
                    var imageUrl = ResolveImageUrl(asset, mediaEmbed.MediaId);
                    var altText = !string.IsNullOrWhiteSpace(mediaEmbed.AltText) ? mediaEmbed.AltText : asset?.AltText;

                    var container = await _apiClient.CreateImageContainerAsync(
                        userId,
                        accessToken,
                        imageUrl,
                        text: postText,
                        altText: altText,
                        isCarouselItem: false,
                        replyToId: replyToId,
                        ct: ct);
                    containerId = container.Id;
                }
                else
                {
                    var childIds = new List<string>();
                    foreach (var mediaEmbed in mediaEmbeds)
                    {
                        var asset = await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == mediaEmbed.MediaId, ct);
                        var imageUrl = ResolveImageUrl(asset, mediaEmbed.MediaId);
                        var altText = !string.IsNullOrWhiteSpace(mediaEmbed.AltText) ? mediaEmbed.AltText : asset?.AltText;

                        var child = await _apiClient.CreateImageContainerAsync(
                            userId,
                            accessToken,
                            imageUrl,
                            text: null,
                            altText: altText,
                            isCarouselItem: true,
                            replyToId: null,
                            ct: ct);

                        await _apiClient.WaitForContainerReadyAsync(child.Id, accessToken, ct: ct);
                        childIds.Add(child.Id);
                    }

                    var carouselContainer = await _apiClient.CreateCarouselContainerAsync(
                        userId,
                        accessToken,
                        childIds,
                        text: postText,
                        replyToId: replyToId,
                        ct: ct);
                    containerId = carouselContainer.Id;
                }

                await _apiClient.WaitForContainerReadyAsync(containerId, accessToken, ct: ct);

                var published = await _apiClient.PublishContainerAsync(userId, accessToken, containerId, ct);

                replyToId = published.Id;
                var postUrl = BuildPostUrl(account.Handle, published.Id);

                publishedPosts.Add(new PublishedPost
                {
                    SegmentIndex = i,
                    RemoteId = published.Id,
                    PublishedText = postText,
                    Url = postUrl
                });

                _logger.LogInformation("Successfully published segment {Index} to Threads. PostId={PostId}", i, published.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish segment {Index} to Threads: {Message}", i, ex.Message);
                return publishedPosts.Count > 0
                    ? PublishResult.Partial(publishedPosts, ex.Message)
                    : PublishResult.Failed(ex.Message, posts: publishedPosts);
            }
        }

        return PublishResult.Succeeded(publishedPosts);
    }

    private string ResolveImageUrl(MediaAsset? asset, Guid mediaId)
    {
        if (asset != null && (asset.FilePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || asset.FilePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
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

        throw new InvalidOperationException($"Unable to resolve public URL for media asset {mediaId}. Set MetaThreads:PublicBaseUrl or App:BaseUrl in configuration.");
    }

    private static string BuildPostUrl(string handle, string postId)
    {
        var cleanHandle = handle.TrimStart('@');
        return !string.IsNullOrWhiteSpace(cleanHandle)
            ? $"https://www.threads.net/@{cleanHandle}/post/{postId}"
            : $"https://www.threads.net/t/{postId}";
    }
}
