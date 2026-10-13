using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.MetaThreads;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.ReplyAgents.Services;

public sealed class ExternalPostResolver : IExternalPostResolver
{
    private static readonly Regex BskyUrlRegex = new(
        @"https?://bsky\.app/profile/[^/]+/post/[^/?#]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ThreadsUrlRegex = new(
        @"https?://(?:www\.)?threads\.(?:net|com)/@[^/]+/post/(?<id>[^/?#]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TwitterUrlRegex = new(
        @"https?://(?:www\.)?(?:twitter\.com|x\.com)/(?<user>[^/]+)/status/(?<id>[0-9]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AppDbContext _db;
    private readonly IBlueskyReplyTargetResolver _blueskyResolver;
    private readonly MetaThreadsApiClient? _threadsApiClient;
    private readonly IConfiguration? _config;
    private readonly ILogger<ExternalPostResolver> _logger;

    public ExternalPostResolver(
        AppDbContext db,
        IBlueskyReplyTargetResolver blueskyResolver,
        ILogger<ExternalPostResolver> logger,
        MetaThreadsApiClient? threadsApiClient = null,
        IConfiguration? config = null)
    {
        _db = db;
        _blueskyResolver = blueskyResolver;
        _logger = logger;
        _threadsApiClient = threadsApiClient;
        _config = config;
    }

    public async Task<PostResolutionResult> ResolveOrCreatePostAsync(
        Guid userId,
        string postUrlOrId,
        string? platformHint = null,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            return PostResolutionResult.Fail("UserId is required.");

        if (string.IsNullOrWhiteSpace(postUrlOrId))
            return PostResolutionResult.Fail("Post URL or ID is required.");

        var trimmed = postUrlOrId.Trim();

        if (Guid.TryParse(trimmed, out var existingPostId))
        {
            var post = await _db.Posts
                .FirstOrDefaultAsync(p => p.Id == existingPostId && (p.UserId == userId || (p.Draft != null && p.Draft.UserId == userId)), ct);

            if (post != null)
                return PostResolutionResult.Ok(post);

            return PostResolutionResult.Fail($"Post with ID {existingPostId} was not found.");
        }

        var existingByRemote = await _db.Posts
            .FirstOrDefaultAsync(p => (p.RemoteId == trimmed || p.Url == trimmed) && (p.UserId == userId || (p.Draft != null && p.Draft.UserId == userId)), ct);

        if (existingByRemote != null)
            return PostResolutionResult.Ok(existingByRemote);

        var isBluesky = BskyUrlRegex.IsMatch(trimmed)
            || trimmed.StartsWith("at://", StringComparison.OrdinalIgnoreCase)
            || string.Equals(platformHint, "Bluesky", StringComparison.OrdinalIgnoreCase);

        if (isBluesky)
        {
            return await ResolveBlueskyPostAsync(userId, trimmed, ct);
        }

        var threadsMatch = ThreadsUrlRegex.Match(trimmed);
        var isThreads = threadsMatch.Success
            || string.Equals(platformHint, "MetaThreads", StringComparison.OrdinalIgnoreCase);

        if (isThreads)
        {
            var mediaId = threadsMatch.Success ? threadsMatch.Groups["id"].Value : trimmed;
            return await ResolveThreadsPostAsync(userId, trimmed, mediaId, ct);
        }

        var twitterMatch = TwitterUrlRegex.Match(trimmed);
        var isTwitter = twitterMatch.Success
            || string.Equals(platformHint, "Twitter", StringComparison.OrdinalIgnoreCase)
            || string.Equals(platformHint, "X", StringComparison.OrdinalIgnoreCase);

        if (isTwitter)
        {
            var tweetId = twitterMatch.Success ? twitterMatch.Groups["id"].Value : trimmed;
            var author = twitterMatch.Success ? twitterMatch.Groups["user"].Value : null;
            return await ResolveTwitterPostAsync(userId, trimmed, tweetId, author, ct);
        }

        return PostResolutionResult.Fail("Unable to recognize or resolve the post URL. Supported platforms: Bluesky, Meta Threads, and Twitter/X.");
    }

    private async Task<PostResolutionResult> ResolveBlueskyPostAsync(
        Guid userId,
        string urlOrUri,
        CancellationToken ct)
    {
        try
        {
            var resolved = await _blueskyResolver.ResolveAsync(urlOrUri, ct);
            if (!resolved.Success || string.IsNullOrWhiteSpace(resolved.ReplyParentUri))
            {
                if (urlOrUri.StartsWith("at://", StringComparison.OrdinalIgnoreCase))
                {
                    var directPost = Post.CreateExternal(
                        userId: userId,
                        platform: "Bluesky",
                        remoteId: urlOrUri,
                        remoteCid: null,
                        publishedText: null,
                        url: null,
                        authorHandle: null);

                    _db.Posts.Add(directPost);
                    await _db.SaveChangesAsync(ct);
                    return PostResolutionResult.Ok(directPost);
                }

                return PostResolutionResult.Fail(resolved.Error ?? "Failed to resolve Bluesky post metadata.");
            }

            var remoteId = resolved.ReplyParentUri;
            var remoteCid = resolved.ReplyParentCid;
            var text = resolved.ReplyParentText;
            var canonicalUrl = resolved.ReplyParentUrl ?? urlOrUri;
            var author = resolved.ReplyParentAuthor;

            var existing = await _db.Posts
                .FirstOrDefaultAsync(p => (p.RemoteId == remoteId || p.Url == canonicalUrl) && (p.UserId == userId || (p.Draft != null && p.Draft.UserId == userId)), ct);

            if (existing != null)
                return PostResolutionResult.Ok(existing);

            var post = Post.CreateExternal(
                userId: userId,
                platform: "Bluesky",
                remoteId: remoteId,
                remoteCid: remoteCid,
                publishedText: text,
                url: canonicalUrl,
                authorHandle: author);

            _db.Posts.Add(post);
            await _db.SaveChangesAsync(ct);

            return PostResolutionResult.Ok(post);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve Bluesky post from {UrlOrUri}", urlOrUri);
            return PostResolutionResult.Fail($"Failed to resolve Bluesky post: {ex.Message}");
        }
    }

    private async Task<PostResolutionResult> ResolveThreadsPostAsync(
        Guid userId,
        string url,
        string mediaId,
        CancellationToken ct)
    {
        var account = await _db.Accounts
            .FirstOrDefaultAsync(a => a.UserId == userId && a.Platform == "MetaThreads", ct);

        var resolvedRemoteId = mediaId;
        string? publishedText = null;
        var resolvedUrl = url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : $"https://www.threads.net/post/{mediaId}";

        if (_threadsApiClient != null && account != null && !string.IsNullOrWhiteSpace(account.CredentialsEncrypted))
        {
            try
            {
                var encryptionKey = _config?["Auth:DbEncryptionKey"] ?? _config?["PLATFORM_CREDENTIALS_KEY"] ?? "";
                var accessToken = CryptoHelper.DecryptString(account.CredentialsEncrypted, encryptionKey);
                var threadsRes = await _threadsApiClient.GetUserThreadsAsync(accessToken, 50, ct);
                var matched = threadsRes.Data?.FirstOrDefault(t =>
                    string.Equals(t.Shortcode, mediaId, StringComparison.OrdinalIgnoreCase)
                    || (t.Permalink != null && t.Permalink.Contains(mediaId, StringComparison.OrdinalIgnoreCase))
                    || string.Equals(t.Id, mediaId, StringComparison.OrdinalIgnoreCase));

                if (matched != null)
                {
                    resolvedRemoteId = matched.Id;
                    if (!string.IsNullOrWhiteSpace(matched.Text))
                    {
                        publishedText = matched.Text;
                    }
                    if (!string.IsNullOrWhiteSpace(matched.Permalink))
                    {
                        resolvedUrl = matched.Permalink;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve Threads shortcode {Shortcode} via Graph API", mediaId);
            }
        }

        var existing = await _db.Posts
            .FirstOrDefaultAsync(p => (p.RemoteId == resolvedRemoteId || p.RemoteId == mediaId || p.Url == resolvedUrl || p.Url == url)
                && (p.UserId == userId || (p.Draft != null && p.Draft.UserId == userId)), ct);

        if (existing != null)
        {
            if (existing.RemoteId != resolvedRemoteId || (string.IsNullOrWhiteSpace(existing.PublishedText) && !string.IsNullOrWhiteSpace(publishedText)))
            {
                existing.RemoteId = resolvedRemoteId;
                if (!string.IsNullOrWhiteSpace(publishedText))
                {
                    existing.PublishedText = publishedText;
                }
                await _db.SaveChangesAsync(ct);
            }
            return PostResolutionResult.Ok(existing);
        }

        var post = Post.CreateExternal(
            userId: userId,
            platform: "MetaThreads",
            remoteId: resolvedRemoteId,
            remoteCid: null,
            publishedText: publishedText,
            url: resolvedUrl,
            authorHandle: account?.Handle);

        _db.Posts.Add(post);
        await _db.SaveChangesAsync(ct);

        return PostResolutionResult.Ok(post);
    }

    private async Task<PostResolutionResult> ResolveTwitterPostAsync(
        Guid userId,
        string url,
        string tweetId,
        string? author,
        CancellationToken ct)
    {
        var canonicalUrl = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? url
            : $"https://x.com/i/status/{tweetId}";

        var existing = await _db.Posts
            .FirstOrDefaultAsync(p => (p.RemoteId == tweetId || p.Url == canonicalUrl || p.Url == url)
                && (p.UserId == userId || (p.Draft != null && p.Draft.UserId == userId)), ct);

        if (existing != null)
        {
            return PostResolutionResult.Ok(existing);
        }

        var post = Post.CreateExternal(
            userId: userId,
            platform: "Twitter",
            remoteId: tweetId,
            remoteCid: null,
            publishedText: null,
            url: canonicalUrl,
            authorHandle: author);

        _db.Posts.Add(post);
        await _db.SaveChangesAsync(ct);

        return PostResolutionResult.Ok(post);
    }
}
