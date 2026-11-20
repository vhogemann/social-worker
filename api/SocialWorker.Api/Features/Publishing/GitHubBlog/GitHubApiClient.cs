using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Publishing.GitHubBlog;

public sealed class GitHubApiClient
{
    private const string ApiBaseUrl = "https://api.github.com";
    private const string ApiVersion = "2022-11-28";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public GitHubApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<GitHubApiResult<GitHubRepoResponse>> VerifyRepositoryAsync(
        string owner,
        string repo,
        string accessToken,
        CancellationToken ct = default)
    {
        var url = $"{ApiBaseUrl}/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}";
        using var request = CreateRequest(HttpMethod.Get, url, accessToken);
        using var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            return GitHubApiResult<GitHubRepoResponse>.Fail(
                $"GitHub repository verification failed ({(int)response.StatusCode}): {body}",
                response.StatusCode);
        }

        var repoInfo = await response.Content.ReadFromJsonAsync<GitHubRepoResponse>(JsonOptions, ct);
        if (repoInfo is null || string.IsNullOrWhiteSpace(repoInfo.FullName))
        {
            return GitHubApiResult<GitHubRepoResponse>.Fail("GitHub returned an invalid repository payload.", response.StatusCode);
        }

        if (repoInfo.Permissions is not null && !repoInfo.Permissions.Push)
        {
            return GitHubApiResult<GitHubRepoResponse>.Fail(
                $"The provided GitHub token does not have push permission to {repoInfo.FullName}. Grant 'Contents: Read and write' on this repository.",
                HttpStatusCode.Forbidden);
        }

        return GitHubApiResult<GitHubRepoResponse>.Ok(repoInfo, response.StatusCode);
    }

    public async Task<GitHubApiResult<bool>> FileExistsAsync(
        string owner,
        string repo,
        string branch,
        string repoPath,
        string accessToken,
        CancellationToken ct = default)
    {
        var encodedPath = EncodeRepoPath(repoPath);
        var url = $"{ApiBaseUrl}/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/contents/{encodedPath}?ref={Uri.EscapeDataString(branch)}";
        using var request = CreateRequest(HttpMethod.Get, url, accessToken);
        using var response = await _http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return GitHubApiResult<bool>.Ok(false, response.StatusCode);
        }

        if (response.IsSuccessStatusCode)
        {
            return GitHubApiResult<bool>.Ok(true, response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return GitHubApiResult<bool>.Fail(
            $"Failed to check existing path '{repoPath}' ({(int)response.StatusCode}): {body}",
            response.StatusCode);
    }

    public async Task<GitHubApiResult<GitHubCreateCommitResponse>> CommitFilesAsync(
        GitHubBlogCredentials credentials,
        IReadOnlyList<GitHubCommitFile> files,
        string commitMessage,
        CancellationToken ct = default)
    {
        if (files.Count == 0)
        {
            return GitHubApiResult<GitHubCreateCommitResponse>.Fail("No files provided to commit.");
        }

        var owner = Uri.EscapeDataString(credentials.Owner);
        var repo = Uri.EscapeDataString(credentials.Repo);
        var branchRef = EncodeRepoPath(credentials.Branch);
        var repoBase = $"{ApiBaseUrl}/repos/{owner}/{repo}";

        var refResult = await SendAndReadAsync<GitHubRefResponse>(
            HttpMethod.Get,
            $"{repoBase}/git/ref/heads/{branchRef}",
            credentials.AccessToken,
            body: null,
            errorContext: $"Failed to read branch '{credentials.Branch}'",
            ct);
        if (!refResult.Success || refResult.Data?.Object is null || string.IsNullOrWhiteSpace(refResult.Data.Object.Sha))
        {
            return GitHubApiResult<GitHubCreateCommitResponse>.Fail(refResult.Error ?? "Could not resolve HEAD commit.", refResult.StatusCode);
        }

        var headCommitSha = refResult.Data.Object.Sha;

        var commitResult = await SendAndReadAsync<GitHubCommitResponse>(
            HttpMethod.Get,
            $"{repoBase}/git/commits/{headCommitSha}",
            credentials.AccessToken,
            body: null,
            errorContext: "Failed to read HEAD commit tree",
            ct);
        if (!commitResult.Success || commitResult.Data?.Tree is null || string.IsNullOrWhiteSpace(commitResult.Data.Tree.Sha))
        {
            return GitHubApiResult<GitHubCreateCommitResponse>.Fail(commitResult.Error ?? "Could not resolve base tree SHA.", commitResult.StatusCode);
        }

        var baseTreeSha = commitResult.Data.Tree.Sha;
        var treeEntries = new List<GitHubTreeEntry>(files.Count);

        foreach (var file in files)
        {
            GitHubCreateBlobRequest blobReq;
            if (file.BinaryContent is not null)
            {
                blobReq = new GitHubCreateBlobRequest(Convert.ToBase64String(file.BinaryContent), "base64");
            }
            else
            {
                blobReq = new GitHubCreateBlobRequest(file.TextContent ?? string.Empty, "utf-8");
            }

            var blobResult = await SendAndReadAsync<GitHubCreateBlobResponse>(
                HttpMethod.Post,
                $"{repoBase}/git/blobs",
                credentials.AccessToken,
                blobReq,
                errorContext: $"Failed to upload blob for '{file.RepoPath}'",
                ct);
            if (!blobResult.Success || blobResult.Data is null || string.IsNullOrWhiteSpace(blobResult.Data.Sha))
            {
                return GitHubApiResult<GitHubCreateCommitResponse>.Fail(blobResult.Error ?? $"Failed to create blob for '{file.RepoPath}'.", blobResult.StatusCode);
            }

            treeEntries.Add(new GitHubTreeEntry(file.RepoPath, "100644", "blob", blobResult.Data.Sha));
        }

        var treeResult = await SendAndReadAsync<GitHubCreateTreeResponse>(
            HttpMethod.Post,
            $"{repoBase}/git/trees",
            credentials.AccessToken,
            new GitHubCreateTreeRequest(baseTreeSha, treeEntries),
            errorContext: "Failed to create Git tree",
            ct);
        if (!treeResult.Success || treeResult.Data is null || string.IsNullOrWhiteSpace(treeResult.Data.Sha))
        {
            return GitHubApiResult<GitHubCreateCommitResponse>.Fail(treeResult.Error ?? "Failed to create Git tree.", treeResult.StatusCode);
        }

        var createCommitResult = await SendAndReadAsync<GitHubCreateCommitResponse>(
            HttpMethod.Post,
            $"{repoBase}/git/commits",
            credentials.AccessToken,
            new GitHubCreateCommitRequest(commitMessage, treeResult.Data.Sha, new[] { headCommitSha }),
            errorContext: "Failed to create Git commit",
            ct);
        if (!createCommitResult.Success || createCommitResult.Data is null || string.IsNullOrWhiteSpace(createCommitResult.Data.Sha))
        {
            return GitHubApiResult<GitHubCreateCommitResponse>.Fail(createCommitResult.Error ?? "Failed to create Git commit.", createCommitResult.StatusCode);
        }

        using var updateRefRequest = CreateRequest(
            HttpMethod.Patch,
            $"{repoBase}/git/refs/heads/{branchRef}",
            credentials.AccessToken,
            new GitHubUpdateRefRequest(createCommitResult.Data.Sha, Force: false));
        using var updateRefResponse = await _http.SendAsync(updateRefRequest, ct);

        if (!updateRefResponse.IsSuccessStatusCode)
        {
            var errBody = await updateRefResponse.Content.ReadAsStringAsync(ct);
            return GitHubApiResult<GitHubCreateCommitResponse>.Fail(
                $"Failed to update branch '{credentials.Branch}' ({(int)updateRefResponse.StatusCode}): {errBody}",
                updateRefResponse.StatusCode);
        }

        return GitHubApiResult<GitHubCreateCommitResponse>.Ok(createCommitResult.Data, updateRefResponse.StatusCode);
    }

    private async Task<GitHubApiResult<T>> SendAndReadAsync<T>(
        HttpMethod method,
        string url,
        string accessToken,
        object? body,
        string errorContext,
        CancellationToken ct)
    {
        using var request = CreateRequest(method, url, accessToken, body);
        using var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(ct);
            return GitHubApiResult<T>.Fail($"{errorContext} ({(int)response.StatusCode}): {errBody}", response.StatusCode);
        }

        var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        if (data is null)
        {
            return GitHubApiResult<T>.Fail($"{errorContext}: empty response body.", response.StatusCode);
        }

        return GitHubApiResult<T>.Ok(data, response.StatusCode);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", ApiVersion);
        request.Headers.UserAgent.ParseAdd("social-worker/1.0");

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private static string EncodeRepoPath(string path)
    {
        return string.Join('/', path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
    }
}
