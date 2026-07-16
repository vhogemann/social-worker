using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Infrastructure;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.Publishing;

public class BlueskyPublisher : IPublisher
{
    private readonly BlueskyApiClient _apiClient;
    private readonly BlueskyContentPreparationService _contentPreparation;
    private readonly BlueskyFacetBuilder _facetBuilder;
    private readonly AppDbContext _db;
    private readonly string _encryptionKey;
    private readonly ILogger<BlueskyPublisher> _logger;

    public string Platform => "Bluesky";

    public BlueskyPublisher(
        HttpClient http,
        IConfiguration config,
        AppDbContext db,
        FileStorageProvider storage,
        ILogger<BlueskyPublisher>? logger = null,
        BlueskyApiClient? apiClient = null,
        BlueskyContentPreparationService? contentPreparation = null,
        BlueskyFacetBuilder? facetBuilder = null)
    {
        _db = db;
        _apiClient = apiClient ?? new BlueskyApiClient(http);
        _contentPreparation = contentPreparation ?? new BlueskyContentPreparationService(db, storage, _apiClient, http);
        _facetBuilder = facetBuilder ?? new BlueskyFacetBuilder();
        _encryptionKey = config["Auth:DbEncryptionKey"] ?? "";
        _logger = logger ?? NullLogger<BlueskyPublisher>.Instance;
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
            var sessionRes = await _apiClient.CreateSessionAsync(account.Handle, appPassword, ct);
            if (!sessionRes.Success || sessionRes.Value is null)
            {
                return new PublishResult { Success = false, ErrorMessage = sessionRes.Error };
            }

            var session = sessionRes.Value;

            var segments = DraftSegmentService.SplitMarkdownIntoSegments(thread.Content ?? "");
            if (segments.Count == 0) return new PublishResult { Success = false, ErrorMessage = "Thread is empty." };

            var replyMetadata = await _db.DraftBlueskyMetadata
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.DraftId == thread.DraftId, ct);

            BlueskyRecordRef? rootRef = null;
            BlueskyRecordRef? parentRef = null;
            if (replyMetadata is not null)
            {
                if (string.IsNullOrWhiteSpace(replyMetadata.ReplyRootUri)
                    || string.IsNullOrWhiteSpace(replyMetadata.ReplyRootCid)
                    || string.IsNullOrWhiteSpace(replyMetadata.ReplyParentUri)
                    || string.IsNullOrWhiteSpace(replyMetadata.ReplyParentCid))
                {
                    return new PublishResult
                    {
                        Success = false,
                        ErrorMessage = "Draft reply target is incomplete. Please set root and parent URI/CID before publishing."
                    };
                }

                rootRef = new BlueskyRecordRef(replyMetadata.ReplyRootUri, replyMetadata.ReplyRootCid);
                parentRef = new BlueskyRecordRef(replyMetadata.ReplyParentUri, replyMetadata.ReplyParentCid);
            }

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

                var prepared = await _contentPreparation.PrepareAsync(text, session.AccessJwt, ct);
                var textFacets = _facetBuilder.Build(prepared.Text);
                var postRecord = new BlueskyPostRecord
                {
                    CreatedAt = DateTime.UtcNow.ToString("O"),
                    Text = textFacets.PlainText,
                    Facets = textFacets.Facets.Count > 0 ? textFacets.Facets : null,
                    Embed = prepared.Embed,
                    Reply = rootRef is not null && parentRef is not null
                        ? new BlueskyReply { Root = rootRef, Parent = parentRef }
                        : null
                };

                _logger.LogInformation("Bluesky segment {SegmentIndex}: textLength={TextLength}, facetCount={FacetCount}", segmentIndex, textFacets.PlainText.Length, textFacets.Facets.Count);

                var postRes = await _apiClient.CreateRecordAsync(session.Did, postRecord, session.AccessJwt, ct);
                if (!postRes.Success || postRes.Value is null)
                {
                    return new PublishResult { Success = false, ErrorMessage = postRes.Error };
                }

                var created = postRes.Value;

                var parts = created.Uri.Split('/');
                var rkey = parts.LastOrDefault();
                var postUrl = $"https://bsky.app/profile/{account.Handle}/post/{rkey}";

                publishedPosts.Add(new PublishedPost
                {
                    SegmentIndex = segmentIndex,
                    RemoteId = created.Uri,
                    Url = postUrl
                });

                segmentIndex++;

                var currentRef = new BlueskyRecordRef(created.Uri, created.Cid);
                rootRef ??= currentRef;
                parentRef = currentRef;
            }

            return new PublishResult { Success = true, Posts = publishedPosts };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected Bluesky publish failure for account {Handle}", account.Handle);
            return new PublishResult { Success = false, ErrorMessage = ex.Message };
        }
    }
}
