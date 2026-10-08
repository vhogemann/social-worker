using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

namespace SocialWorker.Api.Features.Publishing.Twitter;

public class TwitterPublisher : IPublisher
{
    private readonly TwitterApiClient _apiClient;
    private readonly TwitterOAuthService _oauth;
    private readonly TwitterOptions _options;
    private readonly AppDbContext _db;
    private readonly string _encryptionKey;
    private readonly ILogger<TwitterPublisher> _logger;

    public string Platform => "Twitter";

    public TwitterPublisher(
        IConfiguration config,
        IOptions<TwitterOptions> options,
        AppDbContext db,
        TwitterApiClient apiClient,
        TwitterOAuthService oauth,
        ILogger<TwitterPublisher> logger)
    {
        _options = options.Value;
        _db = db;
        _apiClient = apiClient;
        _oauth = oauth;
        _logger = logger;
        _encryptionKey = config["Auth:DbEncryptionKey"] ?? "";
    }

    public async Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_encryptionKey))
        {
            return PublishResult.Failed("Server encryption key not configured.");
        }

        string decrypted;
        try
        {
            decrypted = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
        }
        catch (Exception ex)
        {
            return PublishResult.Failed($"Failed to decrypt credentials: {ex.Message}");
        }

        TwitterAccountCredentials? credentials = null;
        string accessToken = decrypted;

        try
        {
            credentials = JsonSerializer.Deserialize<TwitterAccountCredentials>(decrypted);
            if (credentials != null && !string.IsNullOrWhiteSpace(credentials.AccessToken))
            {
                accessToken = credentials.AccessToken;
            }
        }
        catch
        {
        }

        if (account.TokenExpiresAt.HasValue && account.TokenExpiresAt.Value <= DateTime.UtcNow.AddMinutes(2) && !string.IsNullOrWhiteSpace(credentials?.RefreshToken))
        {
            try
            {
                var refreshed = await _oauth.RefreshTokenAsync(credentials.RefreshToken, ct);
                accessToken = refreshed.AccessToken;

                var updatedCredentials = new TwitterAccountCredentials(
                    AccessToken: refreshed.AccessToken,
                    RefreshToken: refreshed.RefreshToken ?? credentials.RefreshToken,
                    TokenType: refreshed.TokenType ?? "bearer",
                    Scope: refreshed.Scope ?? credentials.Scope);

                account.CredentialsEncrypted = CryptoHelper.EncryptString(JsonSerializer.Serialize(updatedCredentials), _encryptionKey);
                account.TokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn);
                account.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh expiring Twitter token during publish for account {Handle}", account.Handle);
            }
        }

        var doc = ThreadMarkdownDocument.Parse(thread.Content ?? "");
        if (doc.Segments.Count == 0 || (doc.Segments.Count == 1 && string.IsNullOrWhiteSpace(doc.Segments[0].PlainText)))
        {
            return PublishResult.Failed("Thread is empty.");
        }

        var existingPosts = await _db.Posts
            .Where(p => p.PlatformThreadId == thread.Id)
            .OrderBy(p => p.SegmentIndex)
            .ToListAsync(ct);

        var segmentTexts = doc.Segments.Select(s => s.PlainText).ToList();
        var resumePlan = PublishResumePlanner.CreatePlan(segmentTexts, existingPosts, "Twitter");

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
        var handleClean = account.Handle.TrimStart('@');

        for (int i = 0; i < doc.Segments.Count; i++)
        {
            if (i < resumePlan.ResumeFromSegmentIndex)
            {
                continue;
            }

            var segment = doc.Segments[i];
            if (string.IsNullOrWhiteSpace(segment.PlainText))
            {
                continue;
            }

            if (i > resumePlan.ResumeFromSegmentIndex && _options.InterTweetDelayMs > 0)
            {
                await Task.Delay(_options.InterTweetDelayMs, ct);
            }

            try
            {
                var tweet = await _apiClient.CreateTweetAsync(
                    accessToken: accessToken,
                    text: segment.PlainText,
                    inReplyToTweetId: replyToId,
                    ct: ct);

                var postUrl = $"https://x.com/{handleClean}/status/{tweet.Id}";
                var post = new PublishedPost
                {
                    SegmentIndex = i,
                    RemoteId = tweet.Id,
                    PublishedText = segment.PlainText,
                    Url = postUrl
                };

                publishedPosts.Add(post);
                replyToId = tweet.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish segment {Index} to Twitter for account {Handle}", i, account.Handle);
                if (publishedPosts.Count > 0)
                {
                    return PublishResult.Partial(publishedPosts, $"Failed publishing segment {i + 1}: {ex.Message}");
                }
                return PublishResult.Failed($"Failed publishing to Twitter: {ex.Message}");
            }
        }

        return PublishResult.Succeeded(publishedPosts);
    }
}
