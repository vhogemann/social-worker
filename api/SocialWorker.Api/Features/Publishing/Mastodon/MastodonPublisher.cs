using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Publishing.Resilience;
using SocialWorker.Api.Infrastructure.Markdown;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.Publishing.Mastodon;

public sealed class MastodonPublisher : IPublisher
{
    private readonly MastodonApiClient _apiClient;
    private readonly AppDbContext _db;
    private readonly FileStorageProvider? _storage;
    private readonly string _encryptionKey;
    private readonly ILogger<MastodonPublisher> _logger;

    public string Platform => "Mastodon";

    public MastodonPublisher(
        IConfiguration config,
        AppDbContext db,
        MastodonApiClient apiClient,
        ILogger<MastodonPublisher> logger,
        FileStorageProvider? storage = null)
    {
        _db = db;
        _apiClient = apiClient;
        _logger = logger;
        _storage = storage;
        _encryptionKey = config["Auth:DbEncryptionKey"] ?? string.Empty;
    }

    public async Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_encryptionKey))
        {
            return PublishResult.Failed("Server encryption key not configured.");
        }

        MastodonCredentials credentials;
        try
        {
            var decrypted = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
            credentials = JsonSerializer.Deserialize<MastodonCredentials>(decrypted)
                ?? throw new InvalidOperationException("Empty Mastodon credentials.");
        }
        catch (Exception ex)
        {
            return PublishResult.Failed($"Failed to decrypt Mastodon credentials: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(credentials.InstanceUrl) || string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            return PublishResult.Failed("Mastodon account is missing instance URL or access token. Please reconnect.");
        }

        var doc = ThreadMarkdownDocument.Parse(thread.Content ?? string.Empty);
        if (doc.Segments.Count == 0 || (doc.Segments.Count == 1 && string.IsNullOrWhiteSpace(doc.Segments[0].PlainText) && doc.Segments[0].MediaEmbeds.Count == 0 && doc.Segments[0].YouTubeEmbed == null))
        {
            return PublishResult.Failed("Thread is empty.");
        }

        var existingPosts = await _db.Posts
            .Where(p => p.PlatformThreadId == thread.Id)
            .OrderBy(p => p.SegmentIndex)
            .ToListAsync(ct);

        var segmentTexts = doc.Segments.Select(BuildSegmentText).ToList();
        var resumePlan = PublishResumePlanner.CreatePlan(segmentTexts, existingPosts, "Mastodon");

        if (!resumePlan.CanResume)
        {
            return PublishResult.Failed(resumePlan.ErrorMessage!);
        }

        if (resumePlan.IsFullyPublished)
        {
            return PublishResult.Succeeded(existingPosts.Select(MapExistingPost));
        }

        var publishedPosts = resumePlan.ExistingPosts.Select(MapExistingPost).ToList();
        string? replyToId = resumePlan.ExistingPosts.LastOrDefault()?.RemoteId;
        var baseUrl = credentials.InstanceUrl.TrimEnd('/');

        for (int i = 0; i < doc.Segments.Count; i++)
        {
            if (i < resumePlan.ResumeFromSegmentIndex)
            {
                continue;
            }

            var segment = doc.Segments[i];
            var statusText = segmentTexts[i];

            if (string.IsNullOrWhiteSpace(statusText) && segment.MediaEmbeds.Count == 0)
            {
                continue;
            }

            try
            {
                List<string>? mediaIds = null;
                if (segment.MediaEmbeds.Count > 0 && _storage != null)
                {
                    mediaIds = new List<string>();
                    foreach (var mediaRef in segment.MediaEmbeds)
                    {
                        var asset = await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == mediaRef.MediaId, ct);
                        if (asset == null)
                        {
                            continue;
                        }

                        var fullPath = _storage.GetFullPath(asset.FilePath);
                        if (!File.Exists(fullPath))
                        {
                            continue;
                        }

                        var bytes = await File.ReadAllBytesAsync(fullPath, ct);
                        var altText = !string.IsNullOrWhiteSpace(mediaRef.AltText) ? mediaRef.AltText : asset.AltText;
                        var uploadResult = await _apiClient.UploadMediaAsync(
                            baseUrl,
                            credentials.AccessToken,
                            bytes,
                            asset.FileName,
                            asset.MimeType,
                            altText,
                            ct);

                        if (!uploadResult.Success || uploadResult.Data == null)
                        {
                            throw new InvalidOperationException(uploadResult.Error ?? "Failed to upload media to Mastodon.");
                        }

                        mediaIds.Add(uploadResult.Data.Id);
                    }
                }

                var visibility = i == 0 ? "public" : "unlisted";
                var statusRequest = new MastodonStatusRequest(
                    Status: statusText,
                    InReplyToId: replyToId,
                    MediaIds: mediaIds is { Count: > 0 } ? mediaIds : null,
                    Visibility: visibility);

                var idempotencyKey = $"{thread.Id}:{i}";
                var createResult = await _apiClient.CreateStatusAsync(
                    baseUrl,
                    credentials.AccessToken,
                    statusRequest,
                    idempotencyKey,
                    ct);

                if (!createResult.Success || createResult.Data == null)
                {
                    throw new InvalidOperationException(createResult.Error ?? "Failed to create Mastodon status.");
                }

                var status = createResult.Data;
                var postUrl = !string.IsNullOrWhiteSpace(status.Url)
                    ? status.Url
                    : $"{baseUrl}/statuses/{status.Id}";

                publishedPosts.Add(new PublishedPost
                {
                    SegmentIndex = i,
                    RemoteId = status.Id,
                    PublishedText = statusText,
                    Url = postUrl
                });

                replyToId = status.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish segment {Index} to Mastodon for account {Handle}", i, account.Handle);
                if (publishedPosts.Count > 0)
                {
                    return PublishResult.Partial(publishedPosts, $"Failed publishing segment {i + 1}: {ex.Message}");
                }

                return PublishResult.Failed($"Failed publishing to Mastodon: {ex.Message}");
            }
        }

        return PublishResult.Succeeded(publishedPosts);
    }

    private static string BuildSegmentText(ThreadSegmentDocument segment)
    {
        var text = segment.PlainText;
        if (segment.YouTubeEmbed != null)
        {
            text = string.IsNullOrWhiteSpace(text)
                ? segment.YouTubeEmbed.Url
                : $"{text}\n\n{segment.YouTubeEmbed.Url}";
        }

        return text;
    }

    private static PublishedPost MapExistingPost(Post p) => new()
    {
        SegmentIndex = p.SegmentIndex,
        RemoteId = p.RemoteId ?? string.Empty,
        RemoteCid = p.RemoteCid,
        PublishedText = p.PublishedText,
        Url = p.Url ?? string.Empty
    };
}
