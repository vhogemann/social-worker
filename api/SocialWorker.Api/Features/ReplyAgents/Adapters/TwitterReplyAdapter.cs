using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Twitter;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.ReplyAgents.Adapters;

public sealed class TwitterReplyAdapter : IReplyPlatformAdapter
{
    private readonly TwitterApiClient _apiClient;
    private readonly TwitterOAuthService _oauth;
    private readonly string _encryptionKey;
    private readonly AppDbContext? _db;
    private readonly ILogger<TwitterReplyAdapter> _logger;

    public string Platform => "Twitter";

    public TwitterReplyAdapter(
        TwitterApiClient apiClient,
        TwitterOAuthService oauth,
        IConfiguration config,
        ILogger<TwitterReplyAdapter> logger,
        AppDbContext? db = null)
    {
        _apiClient = apiClient;
        _oauth = oauth;
        _logger = logger;
        _db = db;
        _encryptionKey = config["Auth:DbEncryptionKey"] ?? config["PLATFORM_CREDENTIALS_KEY"] ?? "";
    }

    private static readonly System.Text.RegularExpressions.Regex UrlRegex = new(@"https?://[^\s]+", System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public async Task<IReadOnlyList<PlatformCommentItem>> FetchCommentsAsync(
        Post post,
        Account account,
        DateTime since,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(post.RemoteId))
        {
            return Array.Empty<PlatformCommentItem>();
        }

        var accessToken = await GetAccessTokenAsync(account, ct);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning("Cannot fetch Twitter comments: unable to retrieve valid access token for account {Handle}", account.Handle);
            return Array.Empty<PlatformCommentItem>();
        }

        try
        {
            var searchResponse = await _apiClient.SearchRecentConversationAsync(accessToken, post.RemoteId, since, ct);
            if (searchResponse?.Data == null || searchResponse.Data.Count == 0)
            {
                return Array.Empty<PlatformCommentItem>();
            }

            var userMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (searchResponse.Includes?.Users != null)
            {
                foreach (var u in searchResponse.Includes.Users)
                {
                    if (!string.IsNullOrWhiteSpace(u.Id) && !string.IsNullOrWhiteSpace(u.Username))
                    {
                        userMap[u.Id] = u.Username;
                    }
                }
            }

            var comments = new List<PlatformCommentItem>();
            foreach (var item in searchResponse.Data)
            {
                if (string.Equals(item.Id, post.RemoteId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var authorHandle = item.AuthorId != null && userMap.TryGetValue(item.AuthorId, out var uname)
                    ? uname
                    : (item.AuthorId ?? "unknown");

                if (string.Equals(authorHandle, account.Handle, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var createdAt = item.CreatedAt ?? DateTime.UtcNow;
                if (createdAt < since)
                {
                    continue;
                }

                var extractedUrls = ExtractUrls(item.Text);

                comments.Add(new PlatformCommentItem(
                    Platform: "Twitter",
                    RemoteCommentId: item.Id,
                    RemoteParentId: post.RemoteId,
                    RemoteRootId: post.RemoteId,
                    RemoteAuthorHandle: authorHandle,
                    RemoteAuthorId: item.AuthorId ?? authorHandle,
                    Text: item.Text,
                    CreatedAt: createdAt,
                    Depth: 1,
                    ExtractedUrls: extractedUrls,
                    ExtractedImages: Array.Empty<PlatformCommentImageAttachment>()
                ));
            }

            return comments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Twitter comments for post {RemoteId}", post.RemoteId);
            return Array.Empty<PlatformCommentItem>();
        }
    }

    public async Task<PublishReplyResult> PublishReplyAsync(
        Post post,
        Account account,
        PlatformCommentItem targetComment,
        string replyText,
        CancellationToken ct = default)
    {
        var accessToken = await GetAccessTokenAsync(account, ct);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return PublishReplyResult.Failed("Unable to retrieve valid access token.");
        }

        try
        {
            var targetTweetId = !string.IsNullOrWhiteSpace(targetComment.RemoteCommentId)
                ? targetComment.RemoteCommentId
                : post.RemoteId;

            var tweet = await _apiClient.CreateTweetAsync(
                accessToken: accessToken,
                text: replyText,
                inReplyToTweetId: targetTweetId,
                ct: ct);

            var handleClean = account.Handle?.TrimStart('@') ?? "i";
            var replyUrl = $"https://x.com/{handleClean}/status/{tweet.Id}";
            return PublishReplyResult.Succeeded(tweet.Id, replyUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish Twitter reply to comment {CommentId}", targetComment.RemoteCommentId);
            return PublishReplyResult.Failed(ex.Message);
        }
    }

    private async Task<string?> GetAccessTokenAsync(Account account, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_encryptionKey))
        {
            _logger.LogError("Server encryption key not configured for Twitter token retrieval.");
            return null;
        }

        string decrypted;
        try
        {
            decrypted = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt Twitter credentials for account {Handle}", account.Handle);
            return null;
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
                if (_db != null)
                {
                    await _db.SaveChangesAsync(ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh expiring Twitter token for account {Handle}", account.Handle);
            }
        }

        return accessToken;
    }

    private static IReadOnlyList<string> ExtractUrls(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        return UrlRegex.Matches(text)
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
