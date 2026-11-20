using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Publishing.GitHubBlog;

public sealed class GitHubBlogDocumentBuilder
{
    private static readonly Regex MarkdownImageRegex = new(@"!\[(?<alt>[^\]]*)\]\((?<url>[^)\s]+)(?:\s+""[^""]*"")?\)", RegexOptions.Compiled);
    private static readonly Regex LeadingH1Regex = new(@"^\s*#\s+(?<title>[^\r\n]+?)(?:\s+#+\s*)?(?:\r?\n|$)", RegexOptions.Compiled);
    private static readonly Regex YamlKeyRegex = new(@"^(?<key>[A-Za-z0-9_-]+)\s*:\s*(?<value>.*)$", RegexOptions.Compiled);
    private static readonly Regex SlugInvalidCharsRegex = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex YouTubeIdRegex = new(@"(?:youtube\.com/(?:watch\?(?:.*&)?v=|shorts/|live/|embed/)|youtu\.be/)(?<id>[A-Za-z0-9_-]{11})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IReadOnlyList<Guid> ExtractReferencedMediaIds(string rawMarkdown)
    {
        var ids = new List<Guid>();
        var seen = new HashSet<Guid>();
        if (string.IsNullOrWhiteSpace(rawMarkdown))
        {
            return ids;
        }

        var (frontmatterLines, body) = SplitFrontmatter(rawMarkdown);
        foreach (var line in frontmatterLines)
        {
            var match = YamlKeyRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var val = UnquoteYamlScalar(match.Groups["value"].Value);
            if (ThreadSegmentDocument.TryExtractMediaId(val, out var fmMediaId) && seen.Add(fmMediaId))
            {
                ids.Add(fmMediaId);
            }
        }

        foreach (Match match in MarkdownImageRegex.Matches(body))
        {
            var url = match.Groups["url"].Value;
            if (ThreadSegmentDocument.TryExtractMediaId(url, out var mediaId) && seen.Add(mediaId))
            {
                ids.Add(mediaId);
            }
        }

        return ids;
    }

    public string ComputePostPath(
        string rawMarkdown,
        string? fallbackTitle,
        GitHubBlogCredentials credentials,
        DateTime? publishDateUtc = null,
        string? slugSuffix = null)
    {
        var (frontmatterLines, body) = SplitFrontmatter(rawMarkdown ?? string.Empty);
        var existingKeys = ParseFrontmatterScalars(frontmatterLines);
        var effectiveDate = ResolvePublishDate(existingKeys, publishDateUtc ?? DateTime.UtcNow);
        var (title, _) = ResolveTitleAndBody(existingKeys, body, fallbackTitle);
        var baseSlug = ResolveSlug(existingKeys, title);
        var effectiveSlug = ApplySlugSuffix(baseSlug, slugSuffix);
        return credentials.CombineWithSiteRoot($"{credentials.NormalizedPostsDirectory}/{effectiveDate:yyyy-MM-dd}-{effectiveSlug}.md");
    }

    public GitHubBlogBuildResult Build(
        string rawMarkdown,
        string? fallbackTitle,
        GitHubBlogCredentials credentials,
        IReadOnlyDictionary<Guid, GitHubBlogMediaAssetInput> mediaAssets,
        DateTime? publishDateUtc = null,
        string? slugSuffix = null)
    {
        var normalizedInput = (rawMarkdown ?? string.Empty).Replace("\r\n", "\n");
        var (frontmatterLines, rawBody) = SplitFrontmatter(normalizedInput);
        var existingKeys = ParseFrontmatterScalars(frontmatterLines);
        var effectiveDate = ResolvePublishDate(existingKeys, publishDateUtc ?? DateTime.UtcNow);
        var (title, bodyAfterTitle) = ResolveTitleAndBody(existingKeys, rawBody, fallbackTitle);
        var baseSlug = ResolveSlug(existingKeys, title);
        var effectiveSlug = ApplySlugSuffix(baseSlug, slugSuffix);

        var postPath = credentials.CombineWithSiteRoot($"{credentials.NormalizedPostsDirectory}/{effectiveDate:yyyy-MM-dd}-{effectiveSlug}.md");
        var webAssetFolder = $"{credentials.NormalizedAssetsDirectory}/{effectiveDate:yyyy-MM-dd}-{effectiveSlug}";

        var uploads = new List<GitHubBlogMediaUpload>();
        var webPathsByMediaId = new Dictionary<Guid, string>();
        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? firstUploadedWebPath = null;

        string ResolveOrRegisterMedia(Guid mediaId)
        {
            if (webPathsByMediaId.TryGetValue(mediaId, out var existingWebPath))
            {
                return existingWebPath;
            }

            if (!mediaAssets.TryGetValue(mediaId, out var asset))
            {
                return string.Empty;
            }

            var safeFileName = EnsureUniqueFileName(SanitizeFileName(asset.FileName, mediaId), usedFileNames);
            var webPath = $"{webAssetFolder}/{safeFileName}";
            var repoPath = credentials.CombineWithSiteRoot(webPath);
            webPathsByMediaId[mediaId] = webPath;
            firstUploadedWebPath ??= webPath;
            uploads.Add(new GitHubBlogMediaUpload(mediaId, repoPath, asset.FileName, asset.Content));
            return webPath;
        }

        var rewrittenFrontmatterLines = new List<string>(frontmatterLines.Count);
        foreach (var line in frontmatterLines)
        {
            var match = YamlKeyRegex.Match(line);
            if (match.Success)
            {
                var key = match.Groups["key"].Value;
                var rawVal = UnquoteYamlScalar(match.Groups["value"].Value);
                if (ThreadSegmentDocument.TryExtractMediaId(rawVal, out var fmMediaId))
                {
                    var webPath = ResolveOrRegisterMedia(fmMediaId);
                    if (!string.IsNullOrEmpty(webPath))
                    {
                        rewrittenFrontmatterLines.Add($"{key}: /{webPath}");
                        continue;
                    }
                }
            }

            rewrittenFrontmatterLines.Add(line);
        }

        var rewrittenBody = MarkdownImageRegex.Replace(bodyAfterTitle, match =>
        {
            var alt = match.Groups["alt"].Value;
            var url = match.Groups["url"].Value;

            if (ThreadSegmentDocument.TryExtractMediaId(url, out var mediaId))
            {
                var webPath = ResolveOrRegisterMedia(mediaId);
                return string.IsNullOrEmpty(webPath)
                    ? match.Value
                    : $"![{alt}](/{webPath})";
            }

            if (TryExtractYouTubeVideoId(url, out var videoId))
            {
                var label = string.IsNullOrWhiteSpace(alt) ? "Watch on YouTube" : alt;
                return $"[![{label}](https://img.youtube.com/vi/{videoId}/hqdefault.jpg)]({url})";
            }

            return match.Value;
        });

        var finalFrontmatter = BuildMergedFrontmatter(
            rewrittenFrontmatterLines,
            existingKeys,
            title,
            effectiveDate,
            credentials,
            firstUploadedWebPath);

        var trimmedBody = rewrittenBody.Trim();
        var finalMarkdown = string.IsNullOrEmpty(trimmedBody)
            ? $"---\n{finalFrontmatter}\n---\n"
            : $"---\n{finalFrontmatter}\n---\n\n{trimmedBody}\n";

        var postUrl = BuildPostUrl(credentials, effectiveDate, effectiveSlug, postPath);

        return new GitHubBlogBuildResult(
            Title: title,
            Slug: effectiveSlug,
            PostPath: postPath,
            PostUrl: postUrl,
            FinalMarkdown: finalMarkdown,
            PublishDateUtc: effectiveDate,
            MediaUploads: uploads);
    }

    public static (List<string> FrontmatterLines, string Body) SplitFrontmatter(string markdown)
    {
        var normalized = (markdown ?? string.Empty).Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
        {
            return (new List<string>(), normalized);
        }

        for (int i = 1; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed == "---" || trimmed == "...")
            {
                var fmLines = lines.Skip(1).Take(i - 1).ToList();
                var body = string.Join("\n", lines.Skip(i + 1));
                return (fmLines, body);
            }
        }

        return (new List<string>(), normalized);
    }

    public static bool HasUnclosedFrontmatter(string markdown)
    {
        var normalized = (markdown ?? string.Empty).Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        if (lines.Length == 0 || lines[0].Trim() != "---")
        {
            return false;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed == "---" || trimmed == "...")
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryExtractYouTubeVideoId(string? url, out string videoId)
    {
        videoId = string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var match = YouTubeIdRegex.Match(url);
        if (!match.Success)
        {
            return false;
        }

        videoId = match.Groups["id"].Value;
        return !string.IsNullOrEmpty(videoId);
    }

    public static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "post";
        }

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        var ascii = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        var slug = SlugInvalidCharsRegex.Replace(ascii, "-").Trim('-');
        if (slug.Length > 80)
        {
            slug = slug.Substring(0, 80).TrimEnd('-');
        }

        return string.IsNullOrWhiteSpace(slug) ? "post" : slug;
    }

    private static Dictionary<string, string> ParseFrontmatterScalars(IReadOnlyList<string> lines)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || char.IsWhiteSpace(line[0]))
            {
                continue;
            }

            var match = YamlKeyRegex.Match(line);
            if (match.Success)
            {
                result[match.Groups["key"].Value] = UnquoteYamlScalar(match.Groups["value"].Value);
            }
        }

        return result;
    }

    private static DateTime ResolvePublishDate(IReadOnlyDictionary<string, string> scalars, DateTime fallbackUtc)
    {
        if (scalars.TryGetValue("date", out var dateStr) && !string.IsNullOrWhiteSpace(dateStr))
        {
            if (DateTimeOffset.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedOffset))
            {
                return parsedOffset.UtcDateTime;
            }

            if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedDate))
            {
                return DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
            }
        }

        return fallbackUtc.Kind == DateTimeKind.Utc ? fallbackUtc : fallbackUtc.ToUniversalTime();
    }

    private static (string Title, string Body) ResolveTitleAndBody(
        IReadOnlyDictionary<string, string> scalars,
        string body,
        string? fallbackTitle)
    {
        if (scalars.TryGetValue("title", out var fmTitle) && !string.IsNullOrWhiteSpace(fmTitle))
        {
            return (fmTitle.Trim(), body);
        }

        var h1Match = LeadingH1Regex.Match(body);
        if (h1Match.Success)
        {
            var extracted = h1Match.Groups["title"].Value.Trim();
            var remaining = body.Substring(h1Match.Length).TrimStart('\n');
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                return (extracted, remaining);
            }
        }

        if (!string.IsNullOrWhiteSpace(fallbackTitle))
        {
            return (fallbackTitle.Trim(), body);
        }

        return ("Untitled Post", body);
    }

    private static string ResolveSlug(IReadOnlyDictionary<string, string> scalars, string title)
    {
        if (scalars.TryGetValue("slug", out var fmSlug) && !string.IsNullOrWhiteSpace(fmSlug))
        {
            return Slugify(fmSlug);
        }

        return Slugify(title);
    }

    private static string ApplySlugSuffix(string baseSlug, string? suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return baseSlug;
        }

        var cleanSuffix = suffix.Trim();
        return cleanSuffix.StartsWith('-') ? $"{baseSlug}{cleanSuffix}" : $"{baseSlug}-{cleanSuffix}";
    }

    private static string BuildMergedFrontmatter(
        IReadOnlyList<string> existingLines,
        IReadOnlyDictionary<string, string> existingKeys,
        string title,
        DateTime publishDateUtc,
        GitHubBlogCredentials credentials,
        string? firstUploadedMediaRepoPath)
    {
        var output = new List<string>();

        if (!existingKeys.ContainsKey("layout"))
        {
            output.Add($"layout: {credentials.NormalizedDefaultLayout}");
        }

        if (!existingKeys.ContainsKey("title"))
        {
            output.Add($"title: {QuoteYamlString(title)}");
        }

        if (!existingKeys.ContainsKey("date"))
        {
            output.Add($"date: {publishDateUtc:yyyy-MM-dd HH:mm:ss} +0000");
        }

        if (!existingKeys.ContainsKey("author") && !string.IsNullOrWhiteSpace(credentials.Author))
        {
            output.Add($"author: {QuoteYamlString(credentials.Author.Trim())}");
        }

        if (!existingKeys.ContainsKey("categories") && credentials.DefaultCategories is { Count: > 0 })
        {
            var cleanCats = credentials.DefaultCategories
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => QuoteYamlString(c.Trim()))
                .ToList();
            if (cleanCats.Count > 0)
            {
                output.Add($"categories: [{string.Join(", ", cleanCats)}]");
            }
        }

        if (!existingKeys.ContainsKey("image") && !string.IsNullOrWhiteSpace(firstUploadedMediaRepoPath))
        {
            output.Add($"image: /{firstUploadedMediaRepoPath}");
        }

        foreach (var line in existingLines)
        {
            output.Add(line);
        }

        return string.Join("\n", output);
    }

    private static string BuildPostUrl(
        GitHubBlogCredentials credentials,
        DateTime publishDateUtc,
        string slug,
        string postPath)
    {
        var siteBase = credentials.NormalizedSiteBaseUrl;
        if (string.IsNullOrWhiteSpace(siteBase))
        {
            return $"https://github.com/{credentials.Owner}/{credentials.Repo}/blob/{credentials.Branch}/{postPath}";
        }

        var permalink = credentials.NormalizedPermalinkFormat
            .Replace("{year}", publishDateUtc.ToString("yyyy", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{month}", publishDateUtc.ToString("MM", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{day}", publishDateUtc.ToString("dd", CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{slug}", slug, StringComparison.OrdinalIgnoreCase);

        if (!permalink.StartsWith('/'))
        {
            permalink = "/" + permalink;
        }

        return $"{siteBase}{permalink}";
    }

    private static string SanitizeFileName(string? fileName, Guid mediaId)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name))
        {
            return $"media-{mediaId:N}.png";
        }

        var ext = Path.GetExtension(name).ToLowerInvariant();
        var stem = Path.GetFileNameWithoutExtension(name);
        var cleanStem = Slugify(stem);
        if (string.IsNullOrWhiteSpace(cleanStem))
        {
            cleanStem = $"media-{mediaId:N}";
        }

        return string.IsNullOrWhiteSpace(ext) ? cleanStem : $"{cleanStem}{ext}";
    }

    private static string EnsureUniqueFileName(string fileName, HashSet<string> used)
    {
        if (used.Add(fileName))
        {
            return fileName;
        }

        var ext = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        int counter = 2;
        while (true)
        {
            var candidate = $"{stem}-{counter}{ext}";
            if (used.Add(candidate))
            {
                return candidate;
            }
            counter++;
        }
    }

    private static string UnquoteYamlScalar(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') ||
             (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            return trimmed.Substring(1, trimmed.Length - 2).Replace("\\\"", "\"");
        }

        return trimmed;
    }

    private static string QuoteYamlString(string value)
    {
        var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{escaped}\"";
    }
}
