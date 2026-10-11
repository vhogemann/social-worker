using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.ReplyAgents.Adapters;

public sealed record BlueskyCommentMetadata(string Cid, string? RootCid);

public sealed class BlueskyReplyAdapter : IReplyPlatformAdapter
{
    private const string GetPostThreadEndpoint = "https://public.api.bsky.app/xrpc/app.bsky.feed.getPostThread";
    private static readonly Regex UrlRegex = new(@"https?://[^\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly BlueskyApiClient _apiClient;
    private readonly HttpClient _http;
    private readonly BlueskyFacetBuilder _facetBuilder;
    private readonly string _encryptionKey;
    private readonly ILogger<BlueskyReplyAdapter> _logger;

    public string Platform => "Bluesky";

    public BlueskyReplyAdapter(
        BlueskyApiClient apiClient,
        HttpClient http,
        BlueskyFacetBuilder facetBuilder,
        IConfiguration config,
        ILogger<BlueskyReplyAdapter> logger)
    {
        _apiClient = apiClient;
        _http = http;
        _facetBuilder = facetBuilder;
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
            var uri = $"{GetPostThreadEndpoint}?uri={Uri.EscapeDataString(post.RemoteId)}&depth=10&parentHeight=0";
            var res = await _http.GetAsync(uri, ct);
            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Failed to fetch Bluesky thread for post {PostId} ({RemoteId}): {StatusCode} {Error}", post.Id, post.RemoteId, res.StatusCode, err);
                return Array.Empty<PlatformCommentItem>();
            }

            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("thread", out var threadElement))
            {
                return Array.Empty<PlatformCommentItem>();
            }

            var items = new List<PlatformCommentItem>();
            TraverseReplies(threadElement, post.RemoteId, post.RemoteCid, post.RemoteId, post.RemoteCid, depth: 1, since, items);
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while fetching Bluesky comments for post {PostId}", post.Id);
            return Array.Empty<PlatformCommentItem>();
        }
    }

    private void TraverseReplies(
        JsonElement threadNode,
        string rootUri,
        string? rootCid,
        string currentParentUri,
        string? currentParentCid,
        int depth,
        DateTime since,
        List<PlatformCommentItem> items)
    {
        if (threadNode.ValueKind != JsonValueKind.Object) return;

        if (!threadNode.TryGetProperty("replies", out var repliesElement) || repliesElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var replyNode in repliesElement.EnumerateArray())
        {
            if (replyNode.ValueKind != JsonValueKind.Object) continue;

            JsonElement postNode = replyNode;
            if (replyNode.TryGetProperty("post", out var wrappedPost))
            {
                postNode = wrappedPost;
            }

            if (postNode.ValueKind != JsonValueKind.Object) continue;

            var uri = postNode.TryGetProperty("uri", out var uriEl) ? uriEl.GetString() : null;
            var cid = postNode.TryGetProperty("cid", out var cidEl) ? cidEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(uri) || string.IsNullOrWhiteSpace(cid)) continue;

            string authorHandle = "";
            string authorDid = "";
            if (postNode.TryGetProperty("author", out var authorEl))
            {
                authorHandle = authorEl.TryGetProperty("handle", out var hEl) ? hEl.GetString() ?? "" : "";
                authorDid = authorEl.TryGetProperty("did", out var dEl) ? dEl.GetString() ?? "" : "";
            }

            string text = "";
            DateTime createdAt = DateTime.UtcNow;
            var extractedUrls = new List<string>();

            if (postNode.TryGetProperty("record", out var recordEl))
            {
                text = recordEl.TryGetProperty("text", out var tEl) ? tEl.GetString() ?? "" : "";
                if (recordEl.TryGetProperty("createdAt", out var caEl) && DateTime.TryParse(caEl.GetString(), out var dt))
                {
                    createdAt = dt.ToUniversalTime();
                }

                if (recordEl.TryGetProperty("facets", out var facetsEl) && facetsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var facet in facetsEl.EnumerateArray())
                    {
                        if (facet.TryGetProperty("features", out var featsEl) && featsEl.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var feat in featsEl.EnumerateArray())
                            {
                                if (feat.TryGetProperty("$type", out var typeEl) && typeEl.GetString() == "app.bsky.richtext.facet#link"
                                    && feat.TryGetProperty("uri", out var uEl))
                                {
                                    var u = uEl.GetString();
                                    if (!string.IsNullOrWhiteSpace(u) && !extractedUrls.Contains(u))
                                        extractedUrls.Add(u);
                                }
                            }
                        }
                    }
                }
            }

            foreach (Match match in UrlRegex.Matches(text))
            {
                var val = match.Value.TrimEnd('.', ',', '!', '?');
                if (!extractedUrls.Contains(val))
                    extractedUrls.Add(val);
            }

            var images = new List<PlatformCommentImageAttachment>();
            if (postNode.TryGetProperty("embed", out var embedEl))
            {
                if (embedEl.TryGetProperty("$type", out var embedTypeEl) && embedTypeEl.GetString()?.Contains("images") == true
                    && embedEl.TryGetProperty("images", out var imagesEl) && imagesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var img in imagesEl.EnumerateArray())
                    {
                        var imgUrl = img.TryGetProperty("thumb", out var thumbEl) ? thumbEl.GetString()
                            : (img.TryGetProperty("fullsize", out var fsEl) ? fsEl.GetString() : null);
                        var alt = img.TryGetProperty("alt", out var altEl) ? altEl.GetString() : null;

                        if (!string.IsNullOrWhiteSpace(imgUrl))
                        {
                            images.Add(new PlatformCommentImageAttachment(imgUrl, alt));
                        }
                    }
                }
                else if (embedEl.TryGetProperty("external", out var extEl))
                {
                    var extUri = extEl.TryGetProperty("uri", out var extUriEl) ? extUriEl.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(extUri) && !extractedUrls.Contains(extUri))
                    {
                        extractedUrls.Add(extUri);
                    }
                }
            }

            if (createdAt >= since)
            {
                var meta = new BlueskyCommentMetadata(cid, rootCid);
                items.Add(new PlatformCommentItem(
                    Platform: Platform,
                    RemoteCommentId: uri,
                    RemoteParentId: currentParentUri,
                    RemoteRootId: rootUri,
                    RemoteAuthorHandle: authorHandle,
                    RemoteAuthorId: authorDid,
                    Text: text,
                    CreatedAt: createdAt,
                    Depth: depth,
                    ExtractedUrls: extractedUrls,
                    ExtractedImages: images,
                    PlatformMetadata: meta
                ));
            }

            TraverseReplies(replyNode, rootUri, rootCid, uri, cid, depth + 1, since, items);
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
            var appPassword = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
            var sessionRes = await _apiClient.CreateSessionAsync(account.Handle, appPassword, ct);
            if (!sessionRes.Success || sessionRes.Value is null)
            {
                return PublishReplyResult.Failed(sessionRes.Error ?? "Failed to create Bluesky session.");
            }

            var session = sessionRes.Value;
            var meta = targetComment.PlatformMetadata as BlueskyCommentMetadata;
            var parentCid = meta?.Cid ?? "";
            var rootCid = meta?.RootCid ?? post.RemoteCid ?? parentCid;

            if (string.IsNullOrWhiteSpace(parentCid))
            {
                var uri = $"{GetPostThreadEndpoint}?uri={Uri.EscapeDataString(targetComment.RemoteCommentId)}&depth=0";
                var res = await _http.GetAsync(uri, ct);
                if (res.IsSuccessStatusCode)
                {
                    var json = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("thread", out var threadEl) && threadEl.TryGetProperty("post", out var postEl))
                    {
                        parentCid = postEl.TryGetProperty("cid", out var cidEl) ? cidEl.GetString() ?? "" : "";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(rootCid))
            {
                rootCid = post.RemoteCid ?? parentCid;
            }

            var preparedSegment = _facetBuilder.Build(replyText);

            var record = new BlueskyPostRecord
            {
                Text = preparedSegment.PlainText,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                Facets = preparedSegment.Facets,
                Reply = new BlueskyReply
                {
                    Root = new BlueskyRecordRef(targetComment.RemoteRootId, rootCid),
                    Parent = new BlueskyRecordRef(targetComment.RemoteCommentId, parentCid)
                }
            };

            var postResult = await _apiClient.CreateRecordAsync(session.Did, record, session.AccessJwt, ct);
            if (!postResult.Success || postResult.Value is null)
            {
                return PublishReplyResult.Failed(postResult.Error ?? "Failed to post Bluesky reply.");
            }

            var replyUri = postResult.Value.Uri;
            var rkey = replyUri.Contains('/') ? replyUri[(replyUri.LastIndexOf('/') + 1)..] : replyUri;
            var canonicalUrl = $"https://bsky.app/profile/{account.Handle}/post/{rkey}";

            return PublishReplyResult.Succeeded(replyUri, canonicalUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish Bluesky reply to comment {CommentId}", targetComment.RemoteCommentId);
            return PublishReplyResult.Failed(ex.Message);
        }
    }
}
