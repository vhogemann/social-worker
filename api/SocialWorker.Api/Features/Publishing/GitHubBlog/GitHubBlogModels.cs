using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Publishing.GitHubBlog;

public sealed record GitHubBlogCredentials(
    string Owner,
    string Repo,
    string Branch,
    string AccessToken,
    string Preset = "jekyll",
    string PostsDirectory = "_posts",
    string AssetsDirectory = "assets/images/posts",
    string? SiteBaseUrl = null,
    string? PermalinkFormat = "/{year}/{month}/{day}/{slug}/",
    string? DefaultLayout = "post",
    string? Author = null,
    List<string>? DefaultCategories = null,
    string? SiteRootDirectory = null)
{
    public string NormalizedSiteRootDirectory => NormalizeOptionalDirectory(SiteRootDirectory);
    public string NormalizedPostsDirectory => NormalizeRepoDirectory(PostsDirectory, "_posts");
    public string NormalizedAssetsDirectory => NormalizeRepoDirectory(AssetsDirectory, "assets/images/posts");
    public string NormalizedPermalinkFormat => string.IsNullOrWhiteSpace(PermalinkFormat) ? "/{year}/{month}/{day}/{slug}/" : PermalinkFormat.Trim();
    public string NormalizedDefaultLayout => string.IsNullOrWhiteSpace(DefaultLayout) ? "post" : DefaultLayout.Trim();
    public string NormalizedPreset => string.IsNullOrWhiteSpace(Preset) ? "jekyll" : Preset.Trim().ToLowerInvariant();
    public string? NormalizedSiteBaseUrl => string.IsNullOrWhiteSpace(SiteBaseUrl) ? null : SiteBaseUrl.Trim().TrimEnd('/');

    public string CombineWithSiteRoot(string relativePath)
    {
        var cleanPath = relativePath.Replace('\\', '/').Trim('/');
        var root = NormalizedSiteRootDirectory;
        return string.IsNullOrEmpty(root) ? cleanPath : $"{root}/{cleanPath}";
    }

    public static string NormalizeOptionalDirectory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().Replace('\\', '/').Trim('/');
    }

    public static string NormalizeRepoDirectory(string? value, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        candidate = candidate.Replace('\\', '/').Trim('/');
        return string.IsNullOrWhiteSpace(candidate) ? fallback : candidate;
    }
}

public sealed record ConnectGitHubBlogRequest(
    string Owner,
    string Repo,
    string AccessToken,
    string? Branch = null,
    string? Preset = null,
    string? PostsDirectory = null,
    string? AssetsDirectory = null,
    string? SiteBaseUrl = null,
    string? PermalinkFormat = null,
    string? DefaultLayout = null,
    string? Author = null,
    List<string>? DefaultCategories = null,
    List<string>? Tags = null,
    string? SiteRootDirectory = null);

public sealed record GitHubBlogConnectionResponse(
    string ExternalId,
    string Handle,
    string Branch,
    string Preset);

public sealed record GitHubBlogSettingsDto(
    string Owner,
    string Repo,
    string Branch,
    string Preset,
    string SiteRootDirectory,
    string PostsDirectory,
    string AssetsDirectory,
    string? SiteBaseUrl,
    string PermalinkFormat,
    string DefaultLayout,
    string? Author,
    List<string> DefaultCategories)
{
    public GitHubBlogSettingsDto(GitHubBlogCredentials creds)
        : this(
            creds.Owner,
            creds.Repo,
            creds.Branch,
            creds.NormalizedPreset,
            creds.NormalizedSiteRootDirectory,
            creds.NormalizedPostsDirectory,
            creds.NormalizedAssetsDirectory,
            creds.NormalizedSiteBaseUrl,
            creds.NormalizedPermalinkFormat,
            creds.NormalizedDefaultLayout,
            creds.Author,
            creds.DefaultCategories ?? new List<string>())
    {
    }
}

public sealed record UpdateGitHubBlogSettingsRequest(
    string? Owner = null,
    string? Repo = null,
    string? AccessToken = null,
    string? Branch = null,
    string? Preset = null,
    string? SiteRootDirectory = null,
    string? PostsDirectory = null,
    string? AssetsDirectory = null,
    string? SiteBaseUrl = null,
    string? PermalinkFormat = null,
    string? DefaultLayout = null,
    string? Author = null,
    List<string>? DefaultCategories = null);

public sealed record GitHubBlogMediaAssetInput(
    Guid MediaId,
    string FileName,
    byte[] Content);

public sealed record GitHubBlogMediaUpload(
    Guid MediaId,
    string RepoPath,
    string OriginalFileName,
    byte[] Content);

public sealed record GitHubBlogBuildResult(
    string Title,
    string Slug,
    string PostPath,
    string PostUrl,
    string FinalMarkdown,
    DateTime PublishDateUtc,
    IReadOnlyList<GitHubBlogMediaUpload> MediaUploads);

public sealed record GitHubCommitFile(
    string RepoPath,
    string? TextContent = null,
    byte[]? BinaryContent = null)
{
    public static GitHubCommitFile FromText(string repoPath, string content) => new(repoPath, TextContent: content);
    public static GitHubCommitFile FromBinary(string repoPath, byte[] bytes) => new(repoPath, BinaryContent: bytes);
}

public sealed record GitHubApiResult<T>(
    bool Success,
    T? Data = default,
    string? Error = null,
    HttpStatusCode? StatusCode = null)
{
    public static GitHubApiResult<T> Ok(T data, HttpStatusCode? statusCode = null) => new(true, data, null, statusCode);
    public static GitHubApiResult<T> Fail(string error, HttpStatusCode? statusCode = null) => new(false, default, error, statusCode);
}

public sealed record GitHubRepoPermissions(
    [property: JsonPropertyName("push")] bool Push,
    [property: JsonPropertyName("admin")] bool Admin,
    [property: JsonPropertyName("pull")] bool Pull);

public sealed record GitHubRepoResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("full_name")] string FullName,
    [property: JsonPropertyName("default_branch")] string DefaultBranch,
    [property: JsonPropertyName("html_url")] string? HtmlUrl,
    [property: JsonPropertyName("permissions")] GitHubRepoPermissions? Permissions);

public sealed record GitHubRefObject(
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("type")] string? Type);

public sealed record GitHubRefResponse(
    [property: JsonPropertyName("ref")] string Ref,
    [property: JsonPropertyName("object")] GitHubRefObject Object);

public sealed record GitHubCommitTreeRef(
    [property: JsonPropertyName("sha")] string Sha);

public sealed record GitHubCommitResponse(
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("tree")] GitHubCommitTreeRef Tree);

public sealed record GitHubCreateBlobRequest(
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("encoding")] string Encoding);

public sealed record GitHubCreateBlobResponse(
    [property: JsonPropertyName("sha")] string Sha);

public sealed record GitHubTreeEntry(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("sha")] string Sha);

public sealed record GitHubCreateTreeRequest(
    [property: JsonPropertyName("base_tree")] string BaseTree,
    [property: JsonPropertyName("tree")] IReadOnlyList<GitHubTreeEntry> Tree);

public sealed record GitHubCreateTreeResponse(
    [property: JsonPropertyName("sha")] string Sha);

public sealed record GitHubCreateCommitRequest(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("tree")] string Tree,
    [property: JsonPropertyName("parents")] IReadOnlyList<string> Parents);

public sealed record GitHubCreateCommitResponse(
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("html_url")] string? HtmlUrl);

public sealed record GitHubUpdateRefRequest(
    [property: JsonPropertyName("sha")] string Sha,
    [property: JsonPropertyName("force")] bool Force);
