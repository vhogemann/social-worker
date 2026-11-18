using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Mastodon;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.ReplyAgents.Adapters;

public sealed class MastodonReplyAdapter : IReplyPlatformAdapter
{
    private static readonly Regex UrlRegex = new(@"https?://[^\s<>()]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlBreakRegex = new(@"<(?:br\s*/?|/p|/div)>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    private readonly MastodonApiClient _apiClient;
    private readonly string _encryptionKey;
    private readonly ILogger<MastodonReplyAdapter> _logger;

    public string Platform => "Mastodon";

    public MastodonReplyAdapter(
        MastodonApiClient apiClient,
        IConfiguration config,
        ILogger<MastodonReplyAdapter> logger)
    {
        _apiClient = apiClient;
        _encryptionKey = config["Auth:DbEncryptionKey"] ?? config["PLATFORM_CREDENTIALS_KEY"] ?? string.Empty;
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
            var creds = DecryptCredentials(account);
            var contextRes = await _apiClient.GetStatusContextAsync(creds.InstanceUrl, creds.AccessToken, post.RemoteId, ct);
            if (!contextRes.Success || contextRes.Data?.Descendants == null || contextRes.Data.Descendants.Count == 0)
            {
                return Array.Empty<PlatformCommentItem>();
            }

            var rawItems = contextRes.Data.Descendants
                .Where(s => !string.IsNullOrWhiteSpace(s.Id) && !string.Equals(s.Visibility, "direct", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var parentMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in rawItems)
            {
                parentMap[item.Id] = item.InReplyToId ?? post.RemoteId;
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
                var createdAt = item.CreatedAt.UtcDateTime;
                if (createdAt < since) continue;

                var text = StripHtml(item.Content);
                var extractedUrls = new List<string>();
                foreach (Match match in UrlRegex.Matches(text))
                {
                    var val = match.Value.TrimEnd('.', ',', '!', '?');
                    if (!extractedUrls.Contains(val))
                    {
                        extractedUrls.Add(val);
                    }
                }

                var parentId = item.InReplyToId ?? post.RemoteId;
                var depth = CalculateDepth(item.Id);
                var authorHandle = item.Account?.Acct ?? item.Account?.Username ?? string.Empty;
                var authorId = item.Account?.Id ?? authorHandle;

                items.Add(new PlatformCommentItem(
                    Platform: Platform,
                    RemoteCommentId: item.Id,
                    RemoteParentId: parentId,
                    RemoteRootId: post.RemoteId,
                    RemoteAuthorHandle: authorHandle,
                    RemoteAuthorId: authorId,
                    Text: text,
                    CreatedAt: createdAt,
                    Depth: depth,
                    ExtractedUrls: extractedUrls,
                    ExtractedImages: Array.Empty<PlatformCommentImageAttachment>(),
                    PlatformMetadata: string.IsNullOrWhiteSpace(item.Visibility)
                        ? null
                        : new Dictionary<string, string> { ["visibility"] = item.Visibility }
                ));
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Mastodon comments for post {PostId} ({RemoteId})", post.Id, post.RemoteId);
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
            var creds = DecryptCredentials(account);
            string? parentVisibility = null;
            if (targetComment.PlatformMetadata is IReadOnlyDictionary<string, string> meta)
            {
                meta.TryGetValue("visibility", out parentVisibility);
            }
            var visibility = string.Equals(parentVisibility, "unlisted", StringComparison.OrdinalIgnoreCase)
                ? "unlisted"
                : "public";

            var formattedReply = replyText;
            if (!string.IsNullOrWhiteSpace(targetComment.RemoteAuthorHandle))
            {
                var mention = targetComment.RemoteAuthorHandle.StartsWith('@')
                    ? targetComment.RemoteAuthorHandle
                    : $"@{targetComment.RemoteAuthorHandle}";
                if (!formattedReply.Contains(mention, StringComparison.OrdinalIgnoreCase))
                {
                    formattedReply = $"{mention} {formattedReply}";
                }
            }

            var req = new MastodonStatusRequest(
                Status: formattedReply,
                InReplyToId: targetComment.RemoteCommentId,
                Visibility: visibility);

            var res = await _apiClient.CreateStatusAsync(
                creds.InstanceUrl,
                creds.AccessToken,
                req,
                idempotencyKey: $"reply:{targetComment.RemoteCommentId}:{Guid.NewGuid():N}",
                ct: ct);

            if (!res.Success || res.Data == null)
            {
                return PublishReplyResult.Failed(res.Error ?? "Failed to publish Mastodon reply.");
            }

            var url = !string.IsNullOrWhiteSpace(res.Data.Url)
                ? res.Data.Url
                : $"{creds.InstanceUrl.TrimEnd('/')}/statuses/{res.Data.Id}";

            return PublishReplyResult.Succeeded(res.Data.Id, url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish Mastodon reply to comment {CommentId}", targetComment.RemoteCommentId);
            return PublishReplyResult.Failed(ex.Message);
        }
    }

    public async Task<LikeCommentResult> LikeCommentAsync(
        Post post,
        Account account,
        PlatformCommentItem targetComment,
        CancellationToken ct = default)
    {
        try
        {
            var creds = DecryptCredentials(account);
            var res = await _apiClient.FavouriteStatusAsync(creds.InstanceUrl, creds.AccessToken, targetComment.RemoteCommentId, ct);
            return res.Success
                ? LikeCommentResult.Succeeded(res.Data?.Id ?? targetComment.RemoteCommentId)
                : LikeCommentResult.Failed(res.Error ?? "Failed to favourite Mastodon status.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to favourite Mastodon comment {CommentId}", targetComment.RemoteCommentId);
            return LikeCommentResult.Failed(ex.Message);
        }
    }

    internal static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withNewlines = HtmlBreakRegex.Replace(html, "\n");
        var withoutTags = HtmlTagRegex.Replace(withNewlines, string.Empty);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }

    private MastodonCredentials DecryptCredentials(Account account)
    {
        var decrypted = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
        return JsonSerializer.Deserialize<MastodonCredentials>(decrypted)
            ?? throw new InvalidOperationException("Invalid Mastodon credentials.");
    }
}
