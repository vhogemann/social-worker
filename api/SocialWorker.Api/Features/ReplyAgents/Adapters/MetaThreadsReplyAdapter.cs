using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.MetaThreads;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.ReplyAgents.Adapters;

public sealed class MetaThreadsReplyAdapter : IReplyPlatformAdapter
{
    private static readonly Regex UrlRegex = new(@"https?://[^\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly MetaThreadsApiClient _apiClient;
    private readonly string _encryptionKey;
    private readonly ILogger<MetaThreadsReplyAdapter> _logger;

    public string Platform => "MetaThreads";

    public MetaThreadsReplyAdapter(
        MetaThreadsApiClient apiClient,
        IConfiguration config,
        ILogger<MetaThreadsReplyAdapter> logger)
    {
        _apiClient = apiClient;
        _encryptionKey = config["Auth:DbEncryptionKey"] ?? config["PLATFORM_CREDENTIALS_KEY"] ?? "";
        _logger = logger;
    }

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

        try
        {
            var accessToken = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
            MetaThreadsRepliesResponse? repliesRes = null;
            try
            {
                repliesRes = await _apiClient.GetConversationAsync(post.RemoteId, accessToken, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch conversation endpoint for Meta Threads post {RemoteId}, falling back to replies endpoint", post.RemoteId);
            }

            if (repliesRes?.Data == null || repliesRes.Data.Count == 0)
            {
                repliesRes = await _apiClient.GetRepliesAsync(post.RemoteId, accessToken, ct);
            }

            if (repliesRes?.Data == null || repliesRes.Data.Count == 0)
            {
                return Array.Empty<PlatformCommentItem>();
            }

            var rawItems = repliesRes.Data
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .ToList();

            var parentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in rawItems)
            {
                var parentId = item.RepliedTo?.Id ?? post.RemoteId;
                parentMap[item.Id] = parentId;
            }

            int CalculateDepth(string commentId)
            {
                var depth = 1;
                var current = commentId;
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (parentMap.TryGetValue(current, out var parent) && !string.Equals(parent, post.RemoteId, StringComparison.OrdinalIgnoreCase))
                {
                    if (!visited.Add(current) || visited.Count > 20) break;
                    depth++;
                    current = parent;
                }
                return depth;
            }

            var items = new List<PlatformCommentItem>();
            foreach (var item in rawItems)
            {
                var createdAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(item.Timestamp) && DateTime.TryParse(item.Timestamp, out var parsedDt))
                {
                    createdAt = parsedDt.ToUniversalTime();
                }

                if (createdAt < since) continue;

                var text = item.Text ?? "";
                var extractedUrls = new List<string>();
                foreach (Match match in UrlRegex.Matches(text))
                {
                    var val = match.Value.TrimEnd('.', ',', '!', '?');
                    if (!extractedUrls.Contains(val))
                        extractedUrls.Add(val);
                }

                var images = new List<PlatformCommentImageAttachment>();
                if (!string.IsNullOrWhiteSpace(item.MediaUrl) && string.Equals(item.MediaType, "IMAGE", StringComparison.OrdinalIgnoreCase))
                {
                    images.Add(new PlatformCommentImageAttachment(item.MediaUrl, null));
                }

                var parentId = item.RepliedTo?.Id ?? post.RemoteId;
                var rootId = item.RootPost?.Id ?? post.RemoteId;
                var depth = CalculateDepth(item.Id);

                items.Add(new PlatformCommentItem(
                    Platform: Platform,
                    RemoteCommentId: item.Id,
                    RemoteParentId: parentId,
                    RemoteRootId: rootId,
                    RemoteAuthorHandle: item.Username ?? "",
                    RemoteAuthorId: item.Username ?? item.Id,
                    Text: text,
                    CreatedAt: createdAt,
                    Depth: depth,
                    ExtractedUrls: extractedUrls,
                    ExtractedImages: images,
                    PlatformMetadata: null
                ));
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Meta Threads comments for post {PostId} ({RemoteId})", post.Id, post.RemoteId);
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
        try
        {
            var accessToken = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
            var userId = account.ExternalId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return PublishReplyResult.Failed("Meta Threads account is missing ExternalId (User ID).");
            }

            var container = await _apiClient.CreateTextContainerAsync(
                userId,
                accessToken,
                replyText,
                replyToId: targetComment.RemoteCommentId,
                ct: ct);

            await _apiClient.WaitForContainerReadyAsync(container.Id, accessToken, ct: ct);
            var pubRes = await _apiClient.PublishContainerAsync(userId, accessToken, container.Id, ct);

            var replyUrl = $"https://www.threads.net/@{account.Handle}/post/{pubRes.Id}";
            return PublishReplyResult.Succeeded(pubRes.Id, replyUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish Meta Threads reply to comment {CommentId}", targetComment.RemoteCommentId);
            return PublishReplyResult.Failed(ex.Message);
        }
    }

    public Task<LikeCommentResult> LikeCommentAsync(
        Post post,
        Account account,
        PlatformCommentItem targetComment,
        CancellationToken ct = default)
    {
        return Task.FromResult(LikeCommentResult.NotSupported("Meta Threads API does not support liking comments or posts."));
    }
}
