using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Infrastructure;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Publishing.LinkedIn;

public sealed class LinkedInPublishService
{
    private readonly AppDbContext _db;
    private readonly FileStorageProvider _storage;
    private readonly LinkedInApiClient _apiClient;

    public LinkedInPublishService(
        AppDbContext db,
        FileStorageProvider storage,
        LinkedInApiClient apiClient)
    {
        _db = db;
        _storage = storage;
        _apiClient = apiClient;
    }

    public async Task<PublishResult> PublishAsync(
        string content,
        string authorUrn,
        string visibility,
        Guid draftId,
        Guid threadId,
        string token,
        CancellationToken ct)
    {
        try
        {
            var doc = ThreadMarkdownDocument.Parse(content);
            var mediaRefs = doc.Segments.SelectMany(s => s.MediaEmbeds).ToList();
            var assetsMap = new Dictionary<Guid, (string? FilePath, string? MimeType, string? AltText)>();

            foreach (var mediaRef in mediaRefs)
            {
                var asset = await _db.MediaAssets.AsNoTracking().FirstOrDefaultAsync(a => a.Id == mediaRef.MediaId, ct);
                if (asset != null)
                {
                    assetsMap[mediaRef.MediaId] = (asset.FilePath, asset.MimeType, asset.AltText);
                }
            }

            LinkedInPostContract contract;
            try
            {
                contract = LinkedInMarkdownMapper.Map(
                    content,
                    authorUrn,
                    visibility,
                    draftId,
                    threadId,
                    id => assetsMap.TryGetValue(id, out var val) ? val : null);
            }
            catch (LinkedInContractException ex)
            {
                return new PublishResult { Success = false, ErrorMessage = ex.Message };
            }

            string? uploadedMediaUrn = null;
            if (contract.MediaKind == LinkedInMediaKind.Image && contract.Image != null)
            {
                var mediaId = Guid.Parse(contract.Image.MediaAssetId);
                var asset = await _db.MediaAssets.FirstOrDefaultAsync(a => a.Id == mediaId, ct);
                if (asset == null)
                {
                    return new PublishResult { Success = false, ErrorMessage = $"Media asset {mediaId} not found." };
                }

                var fullPath = _storage.GetFullPath(asset.FilePath);
                if (!File.Exists(fullPath))
                {
                    return new PublishResult { Success = false, ErrorMessage = $"Media file {asset.FilePath} not found on disk." };
                }

                var bytes = await File.ReadAllBytesAsync(fullPath, ct);

                var registerRes = await _apiClient.RegisterUploadAsync(authorUrn, token, ct);
                if (!registerRes.Success || registerRes.Value?.Value == null)
                {
                    return new PublishResult { Success = false, ErrorMessage = registerRes.Error ?? "Failed to register upload." };
                }

                var uploadUrl = registerRes.Value.Value.UploadMechanism!.MediaUploadMechanism!.UploadUrl!;
                var headers = registerRes.Value.Value.UploadMechanism.MediaUploadMechanism.Headers;
                var assetUrn = registerRes.Value.Value.Asset!;

                var uploadRes = await _apiClient.UploadBinaryAsync(uploadUrl, bytes, asset.MimeType, headers, ct);
                if (!uploadRes.Success)
                {
                    return new PublishResult { Success = false, ErrorMessage = uploadRes.Error ?? "Failed to upload image binary." };
                }

                uploadedMediaUrn = assetUrn;
            }

            var ugcRequest = LinkedInPayloadMapper.MapToUgcPostRequest(contract, uploadedMediaUrn);

            var publishRes = await _apiClient.PublishPostAsync(ugcRequest, token, ct);
            if (!publishRes.Success || publishRes.Value == null)
            {
                return new PublishResult { Success = false, ErrorMessage = publishRes.Error ?? "Failed to publish post." };
            }

            var remoteId = publishRes.Value;
            var postUrl = $"https://www.linkedin.com/feed/update/{remoteId}";

            var publishedPost = new PublishedPost
            {
                SegmentIndex = contract.SourceSegmentIndex,
                RemoteId = remoteId,
                PublishedText = contract.BodyText,
                Url = postUrl
            };

            return new PublishResult
            {
                Success = true,
                Posts = new List<PublishedPost> { publishedPost }
            };
        }
        catch (Exception ex)
        {
            return new PublishResult { Success = false, ErrorMessage = $"Unexpected failure during LinkedIn publishing: {ex.Message}" };
        }
    }
}
