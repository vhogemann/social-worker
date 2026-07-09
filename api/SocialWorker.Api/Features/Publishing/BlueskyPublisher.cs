using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;

namespace SocialWorker.Api.Features.Publishing;

public class BlueskyPublisher : IPublisher
{
    private readonly HttpClient _http;
    private readonly string _identifier;
    private readonly string _appPassword;

    public string Platform => "Bluesky";

    public BlueskyPublisher(HttpClient http, IConfiguration config)
    {
        _http = http;
        _identifier = config["Bluesky:Identifier"] ?? "";
        _appPassword = config["Bluesky:AppPassword"] ?? "";
    }

    public async Task<PublishResult> PublishAsync(PlatformThread thread, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_identifier) || string.IsNullOrEmpty(_appPassword))
        {
            return new PublishResult
            {
                Success = false,
                ErrorMessage = "Bluesky credentials not configured."
            };
        }

        try
        {
            // 1. Create Session
            var sessionReq = new { identifier = _identifier, password = _appPassword };
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

            // 2. Parse thread content into segments
            var segments = DraftsService.SplitMarkdownIntoSegments(thread.Content ?? "");
            if (segments.Count == 0)
            {
                return new PublishResult { Success = false, ErrorMessage = "Thread is empty." };
            }

            // 3. Post segments sequentially
            JsonObject? rootRef = null;
            JsonObject? parentRef = null;
            string? firstPostUri = null;

            foreach (var segment in segments)
            {
                var text = segment.Trim();
                if (string.IsNullOrEmpty(text)) continue;

                var postRecord = new JsonObject
                {
                    ["$type"] = "app.bsky.feed.post",
                    ["text"] = text,
                    ["createdAt"] = DateTime.UtcNow.ToString("O")
                };

                if (rootRef != null && parentRef != null)
                {
                    postRecord["reply"] = new JsonObject
                    {
                        ["root"] = rootRef.DeepClone(),
                        ["parent"] = parentRef.DeepClone()
                    };
                }

                var createRecordReq = new
                {
                    repo = did,
                    collection = "app.bsky.feed.post",
                    record = postRecord
                };

                var postRes = await _http.PostAsJsonAsync("https://bsky.social/xrpc/com.atproto.repo.createRecord", createRecordReq, ct);
                
                if (!postRes.IsSuccessStatusCode)
                {
                    var err = await postRes.Content.ReadAsStringAsync(ct);
                    return new PublishResult { Success = false, ErrorMessage = $"Failed to post segment to Bluesky: {err}" };
                }

                var postData = await postRes.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct);
                var uri = postData?["uri"]?.GetValue<string>();
                var cid = postData?["cid"]?.GetValue<string>();

                if (uri == null || cid == null)
                {
                    return new PublishResult { Success = false, ErrorMessage = "Invalid post response from Bluesky." };
                }

                firstPostUri ??= uri;

                var currentRef = new JsonObject { ["uri"] = uri, ["cid"] = cid };
                rootRef ??= (JsonObject)currentRef.DeepClone();
                parentRef = (JsonObject)currentRef.DeepClone();
            }

            return new PublishResult
            {
                Success = true,
                RemoteId = firstPostUri
            };
        }
        catch (Exception ex)
        {
            return new PublishResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}
