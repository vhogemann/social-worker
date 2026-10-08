using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Resilience;
using SocialWorker.Api.Infrastructure;
using SocialWorker.Api.Infrastructure.Markdown;
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
        IConfiguration config,
        AppDbContext db,
        BlueskyApiClient apiClient,
        BlueskyContentPreparationService contentPreparation,
        BlueskyFacetBuilder facetBuilder,
        ILogger<BlueskyPublisher> logger)
    {
        _db = db;
        _apiClient = apiClient;
        _contentPreparation = contentPreparation;
        _facetBuilder = facetBuilder;
        _encryptionKey = config["Auth:DbEncryptionKey"] ?? "";
        _logger = logger;
    }

    public async Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_encryptionKey))
        {
            return PublishResult.Failed("Server encryption key not configured.");
        }

        string appPassword;
        try
        {
            appPassword = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
        }
        catch (Exception ex)
        {
            return PublishResult.Failed($"Failed to decrypt credentials: {ex.Message}");
        }

        var publishedPosts = new List<PublishedPost>();
        try
        {
            var sessionRes = await _apiClient.CreateSessionAsync(account.Handle, appPassword, ct);
            if (!sessionRes.Success || sessionRes.Value is null)
            {
                return PublishResult.Failed(sessionRes.Error ?? "Failed to create Bluesky session.");
            }

            var session = sessionRes.Value;

            var doc = ThreadMarkdownDocument.Parse(thread.Content ?? "");
            if (doc.Segments.Count == 0 || (doc.Segments.Count == 1 && string.IsNullOrWhiteSpace(doc.Segments[0].PlainText)))
            {
                return PublishResult.Failed("Thread is empty.");
            }

            var existingPosts = await _db.Posts
                .Where(p => p.PlatformThreadId == thread.Id)
                .OrderBy(p => p.SegmentIndex)
                .ToListAsync(ct);

            var segmentTexts = doc.Segments.Select(s => _facetBuilder.Build(s).PlainText).ToList();
            var resumePlan = PublishResumePlanner.CreatePlan(segmentTexts, existingPosts, "Bluesky");

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
                    return PublishResult.Failed("Draft reply target is incomplete. Please set root and parent URI/CID before publishing.");
                }

                rootRef = new BlueskyRecordRef(replyMetadata.ReplyRootUri, replyMetadata.ReplyRootCid);
                parentRef = new BlueskyRecordRef(replyMetadata.ReplyParentUri, replyMetadata.ReplyParentCid);
            }

            if (resumePlan.ExistingPosts.Count > 0)
            {
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

                if (replyMetadata is null)
                {
                    var first = resumePlan.ExistingPosts[0];
                    rootRef = new BlueskyRecordRef(first.RemoteId ?? "", first.RemoteCid ?? "");
                }
                var last = resumePlan.ExistingPosts[^1];
                parentRef = new BlueskyRecordRef(last.RemoteId ?? "", last.RemoteCid ?? "");
            }

            int segmentIndex = 0;

            foreach (var segment in doc.Segments)
            {
                if (segmentIndex < resumePlan.ResumeFromSegmentIndex)
                {
                    segmentIndex++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(segment.PlainText) && segment.MediaEmbeds.Count == 0 && segment.YouTubeEmbed == null)
                {
                    segmentIndex++;
                    continue;
                }

                var prepared = await _contentPreparation.PrepareAsync(segment, session.AccessJwt, ct);
                var textFacets = _facetBuilder.Build(segment);
                var postRecord = new BlueskyPostRecord
                {
                    CreatedAt = DateTime.UtcNow.ToString("O"),
                    Text = textFacets.PlainText,
                    Facets = textFacets.Facets.Count > 0 ? textFacets.Facets : null,
                    Embed = prepared.Embed,
                    Reply = (rootRef is not null && parentRef is not null)
                        ? new BlueskyReply { Root = rootRef, Parent = parentRef }
                        : null
                };

                _logger.LogInformation("Bluesky segment {SegmentIndex}: textLength={TextLength}, facetCount={FacetCount}", segmentIndex, textFacets.PlainText.Length, textFacets.Facets.Count);

                var postRes = await _apiClient.CreateRecordAsync(session.Did, postRecord, session.AccessJwt, ct);
                if (!postRes.Success || postRes.Value is null)
                {
                    var errorMsg = postRes.Error ?? "Failed to create record on Bluesky.";
                    return publishedPosts.Count > 0
                        ? PublishResult.Partial(publishedPosts, errorMsg)
                        : PublishResult.Failed(errorMsg);
                }

                var created = postRes.Value;

                var parts = created.Uri.Split('/');
                var rkey = parts.LastOrDefault();
                var postUrl = $"https://bsky.app/profile/{account.Handle}/post/{rkey}";

                publishedPosts.Add(new PublishedPost
                {
                    SegmentIndex = segmentIndex,
                    RemoteId = created.Uri,
                    RemoteCid = created.Cid,
                    PublishedText = textFacets.PlainText,
                    Url = postUrl
                });

                segmentIndex++;

                var currentRef = new BlueskyRecordRef(created.Uri, created.Cid);
                rootRef ??= currentRef;
                parentRef = currentRef;
            }

            return PublishResult.Succeeded(publishedPosts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected Bluesky publish failure for account {Handle}", account.Handle);
            return publishedPosts.Count > 0
                ? PublishResult.Partial(publishedPosts, ex.Message)
                : PublishResult.Failed(ex.Message);
        }
    }
}
