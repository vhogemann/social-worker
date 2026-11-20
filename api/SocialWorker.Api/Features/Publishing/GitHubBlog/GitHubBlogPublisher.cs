using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.Publishing.GitHubBlog;

public sealed class GitHubBlogPublisher : IPublisher
{
    private const int MaxCollisionAttempts = 20;

    private readonly AppDbContext _db;
    private readonly GitHubApiClient _apiClient;
    private readonly GitHubBlogDocumentBuilder _documentBuilder;
    private readonly FileStorageProvider? _storage;
    private readonly string _encryptionKey;
    private readonly ILogger<GitHubBlogPublisher> _logger;

    public string Platform => "GitHubBlog";

    public GitHubBlogPublisher(
        IConfiguration config,
        AppDbContext db,
        GitHubApiClient apiClient,
        GitHubBlogDocumentBuilder documentBuilder,
        ILogger<GitHubBlogPublisher> logger,
        FileStorageProvider? storage = null)
    {
        _db = db;
        _apiClient = apiClient;
        _documentBuilder = documentBuilder;
        _logger = logger;
        _storage = storage;
        _encryptionKey = config["Auth:DbEncryptionKey"] ?? string.Empty;
    }

    public async Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_encryptionKey))
        {
            return PublishResult.Failed("Server encryption key not configured.");
        }

        GitHubBlogCredentials credentials;
        try
        {
            var decrypted = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
            credentials = JsonSerializer.Deserialize<GitHubBlogCredentials>(decrypted)
                ?? throw new InvalidOperationException("Empty GitHub Blog credentials.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt GitHub Blog credentials for account {AccountId}", account.Id);
            return PublishResult.Failed($"Failed to decrypt GitHub Blog credentials: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(credentials.Owner) ||
            string.IsNullOrWhiteSpace(credentials.Repo) ||
            string.IsNullOrWhiteSpace(credentials.Branch) ||
            string.IsNullOrWhiteSpace(credentials.AccessToken))
        {
            return PublishResult.Failed("GitHub Blog account is missing repository, branch, or access token. Please reconnect.");
        }

        var existingPosts = await _db.Posts
            .Where(p => p.PlatformThreadId == thread.Id)
            .OrderBy(p => p.SegmentIndex)
            .ToListAsync(ct);

        if (existingPosts.Count > 0)
        {
            return PublishResult.Succeeded(existingPosts.Select(p => new PublishedPost
            {
                SegmentIndex = p.SegmentIndex,
                RemoteId = p.RemoteId ?? string.Empty,
                RemoteCid = p.RemoteCid,
                Url = p.Url ?? string.Empty,
                PublishedText = p.PublishedText
            }));
        }

        var rawContent = thread.Content ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return PublishResult.Failed("Blog post content is empty.");
        }

        var fallbackTitle = thread.Draft?.Title;
        if (string.IsNullOrWhiteSpace(fallbackTitle) && thread.DraftId != Guid.Empty)
        {
            fallbackTitle = await _db.Drafts
                .AsNoTracking()
                .Where(d => d.Id == thread.DraftId)
                .Select(d => d.Title)
                .FirstOrDefaultAsync(ct);
        }

        var publishDateUtc = DateTime.UtcNow;
        var slugSuffix = await ResolveNonCollidingSuffixAsync(rawContent, fallbackTitle, credentials, publishDateUtc, ct);
        if (slugSuffix is null)
        {
            return PublishResult.Failed("Could not determine a unique post filename in the GitHub repository.");
        }

        var mediaInputs = await LoadReferencedMediaAsync(rawContent, ct);
        var built = _documentBuilder.Build(
            rawContent,
            fallbackTitle,
            credentials,
            mediaInputs,
            publishDateUtc,
            string.IsNullOrEmpty(slugSuffix) ? null : slugSuffix);

        var commitFiles = new List<GitHubCommitFile>(built.MediaUploads.Count + 1);
        foreach (var upload in built.MediaUploads)
        {
            commitFiles.Add(GitHubCommitFile.FromBinary(upload.RepoPath, upload.Content));
        }

        commitFiles.Add(GitHubCommitFile.FromText(built.PostPath, built.FinalMarkdown));

        var commitMessage = $"Publish post: {built.Title}";
        var commitResult = await _apiClient.CommitFilesAsync(credentials, commitFiles, commitMessage, ct);
        if (!commitResult.Success || commitResult.Data is null)
        {
            return PublishResult.Failed(commitResult.Error ?? "Failed to commit post to GitHub.");
        }

        return PublishResult.Succeeded(new[]
        {
            new PublishedPost
            {
                SegmentIndex = 0,
                RemoteId = commitResult.Data.Sha,
                RemoteCid = built.PostPath,
                Url = built.PostUrl,
                PublishedText = built.FinalMarkdown
            }
        });
    }

    private async Task<string?> ResolveNonCollidingSuffixAsync(
        string rawContent,
        string? fallbackTitle,
        GitHubBlogCredentials credentials,
        DateTime publishDateUtc,
        CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxCollisionAttempts; attempt++)
        {
            var suffix = attempt == 1 ? string.Empty : attempt.ToString();
            var candidatePath = _documentBuilder.ComputePostPath(
                rawContent,
                fallbackTitle,
                credentials,
                publishDateUtc,
                string.IsNullOrEmpty(suffix) ? null : suffix);

            var existsResult = await _apiClient.FileExistsAsync(
                credentials.Owner,
                credentials.Repo,
                credentials.Branch,
                candidatePath,
                credentials.AccessToken,
                ct);

            if (!existsResult.Success)
            {
                _logger.LogError("GitHub collision check failed for {Path}: {Error}", candidatePath, existsResult.Error);
                return null;
            }

            if (!existsResult.Data)
            {
                return suffix;
            }
        }

        return null;
    }

    private async Task<Dictionary<Guid, GitHubBlogMediaAssetInput>> LoadReferencedMediaAsync(string rawContent, CancellationToken ct)
    {
        var result = new Dictionary<Guid, GitHubBlogMediaAssetInput>();
        if (_storage is null)
        {
            return result;
        }

        var mediaIds = _documentBuilder.ExtractReferencedMediaIds(rawContent);
        if (mediaIds.Count == 0)
        {
            return result;
        }

        var assets = await _db.MediaAssets
            .AsNoTracking()
            .Where(a => mediaIds.Contains(a.Id))
            .ToListAsync(ct);

        foreach (var asset in assets)
        {
            var fullPath = _storage.GetFullPath(asset.FilePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(fullPath, ct);
            result[asset.Id] = new GitHubBlogMediaAssetInput(asset.Id, asset.FileName, bytes);
        }

        return result;
    }
}
