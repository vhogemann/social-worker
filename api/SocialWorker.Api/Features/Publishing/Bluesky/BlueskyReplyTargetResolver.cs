using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Publishing.Bluesky;

public sealed record BlueskyReplyTargetResolutionResult(
    bool Success,
    string? Error,
    string? ReplyRootUri = null,
    string? ReplyRootCid = null,
    string? ReplyParentUri = null,
    string? ReplyParentCid = null,
    string? ReplyParentUrl = null,
    string? ReplyParentAuthor = null,
    string? ReplyParentText = null,
    string? ReplyParentAvatarUrl = null
);

public sealed class BlueskyReplyTargetResolver : IBlueskyReplyTargetResolver
{
    private const string ResolveHandleEndpoint = "https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle";
    private const string GetPostThreadEndpoint = "https://public.api.bsky.app/xrpc/app.bsky.feed.getPostThread";
    private static readonly Regex StrictBskyPostUrl = new(
        "^https://bsky\\.app/profile/(?<handle>[^/]+)/post/(?<rkey>[^/?#]+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private readonly HttpClient _http;

    public BlueskyReplyTargetResolver(HttpClient http)
    {
        _http = http;
    }

    public async Task<BlueskyReplyTargetResolutionResult> ResolveAsync(string url, CancellationToken ct)
    {
        if (!TryParseStrictPostUrl(url, out var handle, out var rkey, out var parseError))
        {
            return new BlueskyReplyTargetResolutionResult(false, parseError);
        }

        var didResult = await ResolveDidAsync(handle, ct);
        if (!didResult.Success || didResult.Did is null)
        {
            return new BlueskyReplyTargetResolutionResult(false, didResult.Error);
        }

        var postUri = $"at://{didResult.Did}/app.bsky.feed.post/{rkey}";
        var threadResult = await ResolveThreadPostAsync(postUri, ct);
        if (!threadResult.Success || threadResult.Post is null)
        {
            return new BlueskyReplyTargetResolutionResult(false, threadResult.Error);
        }

        if (string.IsNullOrWhiteSpace(threadResult.Post.Uri)
            || string.IsNullOrWhiteSpace(threadResult.Post.Cid)
            || string.IsNullOrWhiteSpace(threadResult.Post.AuthorHandle)
            || string.IsNullOrWhiteSpace(threadResult.Post.Text)
            || string.IsNullOrWhiteSpace(threadResult.Post.AuthorAvatarUrl))
        {
            return new BlueskyReplyTargetResolutionResult(false, "Resolved Bluesky post is missing required preview metadata.");
        }

        var replyRootUri = string.IsNullOrWhiteSpace(threadResult.Post.ReplyRootUri)
            ? threadResult.Post.Uri
            : threadResult.Post.ReplyRootUri;
        var replyRootCid = string.IsNullOrWhiteSpace(threadResult.Post.ReplyRootCid)
            ? threadResult.Post.Cid
            : threadResult.Post.ReplyRootCid;

        var canonicalRkey = GetPostRkey(threadResult.Post.Uri) ?? rkey;
        var canonicalUrl = $"https://bsky.app/profile/{threadResult.Post.AuthorHandle}/post/{canonicalRkey}";

        return new BlueskyReplyTargetResolutionResult(
            true,
            null,
            replyRootUri,
            replyRootCid,
            threadResult.Post.Uri,
            threadResult.Post.Cid,
            canonicalUrl,
            threadResult.Post.AuthorHandle,
            threadResult.Post.Text,
            threadResult.Post.AuthorAvatarUrl
        );
    }

    public async Task<string?> ResolveThreadContextAsync(string url, CancellationToken ct)
    {
        if (!TryParseStrictPostUrl(url, out var handle, out var rkey, out _))
        {
            return null;
        }

        var didResult = await ResolveDidAsync(handle, ct);
        if (!didResult.Success || didResult.Did is null)
        {
            return null;
        }

        var postUri = $"at://{didResult.Did}/app.bsky.feed.post/{rkey}";
        var uri = $"{GetPostThreadEndpoint}?uri={Uri.EscapeDataString(postUri)}&depth=1&parentHeight=50";
        var response = await _http.GetAsync(uri, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("thread", out var threadElement))
        {
            return null;
        }

        var lines = new List<string>();
        CollectParentChain(threadElement, lines);
        if (lines.Count == 0)
        {
            return null;
        }

        const int maxChars = 6000;
        var combined = string.Join("\n\n", lines);
        if (combined.Length > maxChars)
        {
            combined = combined[..maxChars] + "\n\n(truncated)";
        }

        return combined;
    }

    private static void CollectParentChain(JsonElement node, List<string> lines)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (node.TryGetProperty("parent", out var parentNode))
        {
            CollectParentChain(parentNode, lines);
        }

        var postNode = node;
        if (node.TryGetProperty("post", out var wrappedPost))
        {
            postNode = wrappedPost;
        }

        if (postNode.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var handle = postNode.TryGetProperty("author", out var author)
            && author.TryGetProperty("handle", out var handleElement)
            ? handleElement.GetString()
            : null;

        var uri = postNode.TryGetProperty("uri", out var uriElement)
            ? uriElement.GetString()
            : null;

        var text = postNode.TryGetProperty("record", out var record)
            && record.TryGetProperty("text", out var textElement)
            ? textElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(handle) || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var sanitizedText = text.Replace("\r", " ").Replace("\n", " ").Trim();
        var canonicalUrl = ToCanonicalUrl(uri, handle);
        if (string.IsNullOrWhiteSpace(canonicalUrl))
        {
            lines.Add($"@{handle}: {sanitizedText}");
        }
        else
        {
            lines.Add($"@{handle} ({canonicalUrl}): {sanitizedText}");
        }
    }

    private static string? ToCanonicalUrl(string? uri, string handle)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        var rkey = GetPostRkey(uri);
        if (string.IsNullOrWhiteSpace(rkey))
        {
            return null;
        }

        return $"https://bsky.app/profile/{handle}/post/{rkey}";
    }

    private static bool TryParseStrictPostUrl(string url, out string handle, out string rkey, out string error)
    {
        handle = string.Empty;
        rkey = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            error = "A Bluesky post URL is required.";
            return false;
        }

        var match = StrictBskyPostUrl.Match(url.Trim());
        if (!match.Success)
        {
            error = "Only URLs in the format https://bsky.app/profile/<handle>/post/<rkey> are supported.";
            return false;
        }

        handle = match.Groups["handle"].Value;
        rkey = match.Groups["rkey"].Value;
        return true;
    }

    private async Task<(bool Success, string? Did, string? Error)> ResolveDidAsync(string handle, CancellationToken ct)
    {
        var uri = $"{ResolveHandleEndpoint}?handle={Uri.EscapeDataString(handle)}";
        var response = await _http.GetAsync(uri, ct);
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, "Unable to resolve Bluesky handle.");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("did", out var didElement))
        {
            return (false, null, "Handle resolution response is missing DID.");
        }

        var did = didElement.GetString();
        if (string.IsNullOrWhiteSpace(did))
        {
            return (false, null, "Handle resolution returned an empty DID.");
        }

        return (true, did, null);
    }

    private async Task<(bool Success, BlueskyResolvedPost? Post, string? Error)> ResolveThreadPostAsync(string postUri, CancellationToken ct)
    {
        var uri = $"{GetPostThreadEndpoint}?uri={Uri.EscapeDataString(postUri)}&depth=1";
        var response = await _http.GetAsync(uri, ct);
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, "Unable to fetch Bluesky post thread.");
        }

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("thread", out var threadElement))
        {
            return (false, null, "Thread response is missing thread data.");
        }

        var post = ExtractPost(threadElement, postUri);
        if (post is null)
        {
            return (false, null, "Unable to resolve a valid post from the supplied Bluesky URL.");
        }

        return (true, post, null);
    }

    private static BlueskyResolvedPost? ExtractPost(JsonElement node, string expectedUri)
    {
        if (node.TryGetProperty("post", out var postNode))
        {
            return ParsePost(postNode, expectedUri);
        }

        return ParsePost(node, expectedUri);
    }

    private static BlueskyResolvedPost? ParsePost(JsonElement postNode, string expectedUri)
    {
        var uri = postNode.TryGetProperty("uri", out var uriElement) ? uriElement.GetString() : null;
        if (!string.Equals(uri, expectedUri, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var cid = postNode.TryGetProperty("cid", out var cidElement) ? cidElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(cid))
        {
            return null;
        }

        var authorHandle = postNode.TryGetProperty("author", out var authorElement)
            && authorElement.TryGetProperty("handle", out var handleElement)
            ? handleElement.GetString()
            : null;
        var authorAvatar = postNode.TryGetProperty("author", out authorElement)
            && authorElement.TryGetProperty("avatar", out var avatarElement)
            ? avatarElement.GetString()
            : null;

        var text = postNode.TryGetProperty("record", out var recordElement)
            && recordElement.TryGetProperty("text", out var textElement)
            ? textElement.GetString()
            : null;

        string? replyRootUri = null;
        string? replyRootCid = null;
        if (postNode.TryGetProperty("record", out recordElement)
            && recordElement.TryGetProperty("reply", out var replyElement)
            && replyElement.TryGetProperty("root", out var rootElement))
        {
            if (rootElement.TryGetProperty("uri", out var rootUriElement))
            {
                replyRootUri = rootUriElement.GetString();
            }

            if (rootElement.TryGetProperty("cid", out var rootCidElement))
            {
                replyRootCid = rootCidElement.GetString();
            }
        }

        if (string.IsNullOrWhiteSpace(uri)
            || string.IsNullOrWhiteSpace(authorHandle)
            || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return new BlueskyResolvedPost(
            uri,
            cid,
            authorHandle,
            text,
            authorAvatar,
            replyRootUri,
            replyRootCid
        );
    }

    private static string? GetPostRkey(string uri)
    {
        var parts = uri.Split('/');
        return parts.Length == 0 ? null : parts[^1];
    }

    private sealed record BlueskyResolvedPost(
        string Uri,
        string Cid,
        string AuthorHandle,
        string Text,
        string? AuthorAvatarUrl,
        string? ReplyRootUri,
        string? ReplyRootCid
    );
}
