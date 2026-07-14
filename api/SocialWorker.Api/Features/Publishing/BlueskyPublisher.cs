using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Infrastructure;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.Publishing;

public class BlueskyPublisher : IPublisher
{
    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly FileStorageProvider _storage;
    private readonly string _encryptionKey;

    public string Platform => "Bluesky";

    public BlueskyPublisher(HttpClient http, IConfiguration config, AppDbContext db, FileStorageProvider storage)
    {
        _http = http;
        _db = db;
        _storage = storage;
        _encryptionKey = config["Auth:DbEncryptionKey"] ?? "";
    }

    public async Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_encryptionKey))
        {
            return new PublishResult { Success = false, ErrorMessage = "Server encryption key not configured." };
        }

        string appPassword;
        try
        {
            appPassword = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
        }
        catch (Exception ex)
        {
            return new PublishResult { Success = false, ErrorMessage = $"Failed to decrypt credentials: {ex.Message}" };
        }

        try
        {
            var sessionReq = new { identifier = account.Handle, password = appPassword };
            var sessionRes = await _http.PostAsJsonAsync("https://bsky.social/xrpc/com.atproto.server.createSession", sessionReq, ct);
            
            if (!sessionRes.IsSuccessStatusCode)
            {
                var err = await sessionRes.Content.ReadAsStringAsync(ct);
                return new PublishResult { Success = false, ErrorMessage = $"Failed to authenticate with Bluesky: {err}" };
            }

            var sessionData = await sessionRes.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
            var accessJwt = sessionData?["accessJwt"]?.GetValue<string>();
            var did = sessionData?["did"]?.GetValue<string>();

            if (accessJwt == null || did == null)
            {
                return new PublishResult { Success = false, ErrorMessage = "Invalid session response from Bluesky." };
            }

            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessJwt);

            var segments = DraftsService.SplitMarkdownIntoSegments(thread.Content ?? "");
            if (segments.Count == 0) return new PublishResult { Success = false, ErrorMessage = "Thread is empty." };

            JsonObject? rootRef = null;
            JsonObject? parentRef = null;
            var publishedPosts = new List<PublishedPost>();
            int segmentIndex = 0;

            foreach (var segment in segments)
            {
                var text = segment.Trim();
                if (string.IsNullOrEmpty(text)) 
                {
                    segmentIndex++;
                    continue;
                }

                var imagesList = new JsonArray();
                var matches = SharedPatterns.MediaRegex.Matches(text);
                
                foreach (Match match in matches)
                {
                    var altText = match.Groups[1].Value;
                    if (Guid.TryParse(match.Groups[2].Value, out var mediaId))
                    {
                        var asset = await _db.MediaAssets.FirstOrDefaultAsync(a => a.Id == mediaId, ct);
                        if (asset != null)
                        {
                            var fullPath = _storage.GetFullPath(asset.FilePath);
                            if (File.Exists(fullPath))
                            {
                                var fileBytes = await File.ReadAllBytesAsync(fullPath, ct);
                                using var content = new ByteArrayContent(fileBytes);
                                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(asset.MimeType);
                                
                                var uploadRes = await _http.PostAsync("https://bsky.social/xrpc/com.atproto.repo.uploadBlob", content, ct);
                                if (uploadRes.IsSuccessStatusCode)
                                {
                                    var uploadData = await uploadRes.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
                                    var blob = uploadData?["blob"];
                                    if (blob != null)
                                    {
                                        imagesList.Add(new JsonObject
                                        {
                                            ["alt"] = !string.IsNullOrEmpty(asset.AltText) ? asset.AltText : altText,
                                            ["image"] = blob.DeepClone()
                                        });
                                    }
                                }
                            }
                        }
                    }
                }

                // Remove the image markdown tags from the text before posting
                text = SharedPatterns.MediaRegex.Replace(text, "").Trim();

                // Detect YouTube markdown embed and build app.bsky.embed.external
                JsonObject? externalEmbed = null;
                var ytMatch = SharedPatterns.YoutubeMarkdownRegex.Match(text);
                if (ytMatch.Success)
                {
                    var ytTitle = ytMatch.Groups[1].Value;
                    var ytUrl = ytMatch.Groups[2].Value;
                    text = SharedPatterns.YoutubeMarkdownRegex.Replace(text, "").Trim();

                    var externalNode = new JsonObject
                    {
                        ["uri"] = ytUrl,
                        ["title"] = string.IsNullOrEmpty(ytTitle) ? ytUrl : ytTitle,
                        ["description"] = ""
                    };

                    try
                    {
                        var oEmbedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(ytUrl)}&format=json";
                        var oEmbedRes = await _http.GetAsync(oEmbedUrl, ct);
                        if (oEmbedRes.IsSuccessStatusCode)
                        {
                            var oEmbed = await oEmbedRes.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
                            var oEmbedTitle = oEmbed?["title"]?.GetValue<string>();
                            var thumbUrl = oEmbed?["thumbnail_url"]?.GetValue<string>();

                            if (!string.IsNullOrEmpty(oEmbedTitle))
                                externalNode["title"] = oEmbedTitle;

                            if (!string.IsNullOrEmpty(thumbUrl))
                            {
                                var thumbRes = await _http.GetAsync(thumbUrl, ct);
                                if (thumbRes.IsSuccessStatusCode)
                                {
                                    var thumbBytes = await thumbRes.Content.ReadAsByteArrayAsync(ct);
                                    var mimeType = thumbRes.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                                    using var thumbContent = new ByteArrayContent(thumbBytes);
                                    thumbContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
                                    var blobRes = await _http.PostAsync("https://bsky.social/xrpc/com.atproto.repo.uploadBlob", thumbContent, ct);
                                    if (blobRes.IsSuccessStatusCode)
                                    {
                                        var blobData = await blobRes.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
                                        var blob = blobData?["blob"];
                                        if (blob != null)
                                            externalNode["thumb"] = blob.DeepClone();
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // oEmbed fetch failed — post with URL and title only, no thumbnail
                    }

                    externalEmbed = new JsonObject
                    {
                        ["$type"] = "app.bsky.embed.external",
                        ["external"] = externalNode
                    };
                }

                var postRecord = new JsonObject
                {
                    ["$type"] = "app.bsky.feed.post",
                    ["text"] = text,
                    ["createdAt"] = DateTime.UtcNow.ToString("O")
                };

                if (externalEmbed != null)
                {
                    postRecord["embed"] = externalEmbed;
                }
                else if (imagesList.Count > 0)
                {
                    postRecord["embed"] = new JsonObject
                    {
                        ["$type"] = "app.bsky.embed.images",
                        ["images"] = imagesList
                    };
                }

                if (rootRef != null && parentRef != null)
                {
                    postRecord["reply"] = new JsonObject
                    {
                        ["root"] = rootRef.DeepClone(),
                        ["parent"] = parentRef.DeepClone()
                    };
                }

                var createRecordReq = new { repo = did, collection = "app.bsky.feed.post", record = postRecord };
                var postRes = await _http.PostAsJsonAsync("https://bsky.social/xrpc/com.atproto.repo.createRecord", createRecordReq, ct);
                
                if (!postRes.IsSuccessStatusCode)
                {
                    var err = await postRes.Content.ReadAsStringAsync(ct);
                    return new PublishResult { Success = false, ErrorMessage = $"Failed to post segment to Bluesky: {err}" };
                }

                var postData = await postRes.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
                var uri = postData?["uri"]?.GetValue<string>();
                var cid = postData?["cid"]?.GetValue<string>();

                if (uri == null || cid == null) return new PublishResult { Success = false, ErrorMessage = "Invalid post response from Bluesky." };

                var parts = uri.Split('/');
                var rkey = parts.LastOrDefault();
                var postUrl = $"https://bsky.app/profile/{account.Handle}/post/{rkey}";

                publishedPosts.Add(new PublishedPost
                {
                    SegmentIndex = segmentIndex,
                    RemoteId = uri,
                    Url = postUrl
                });

                segmentIndex++;

                var currentRef = new JsonObject { ["uri"] = uri, ["cid"] = cid };
                rootRef ??= (JsonObject)currentRef.DeepClone();
                parentRef = (JsonObject)currentRef.DeepClone();
            }

            return new PublishResult { Success = true, Posts = publishedPosts };
        }
        catch (Exception ex)
        {
            return new PublishResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}
