using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure.Markdown;

namespace SocialWorker.Api.Features.Publishing.GitHubBlog.Validation;

public sealed class GitHubBlogDraftValidator : IDraftValidator
{
    private static readonly Regex PlaceholderLinkRegex = new(
        @"(?<!\!)\[(?i:\s*|link|url|insert\s+link|insira\s+o\s+link|placeholder)\]\([^)]+\)|(?<!\!)\[[^\]\n]+\]\((?i:\s*|#|placeholder|url|link)\)|(?<!\!)\[(?=[^\]\n]{0,80}(?i:source\s+link|insert\s+link|placeholder))[^\]\n]+\](?!\()",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MarkdownImageRegex = new(@"!\[(?<alt>[^\]]*)\]\((?<url>[^)\s]+)(?:\s+""[^""]*"")?\)", RegexOptions.Compiled);
    private static readonly Regex LeadingH1Regex = new(@"^\s*#\s+[^\r\n]+", RegexOptions.Compiled);
    private static readonly Regex FrontmatterTitleRegex = new(@"^title\s*:\s*\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ValidateDraftResult Validate(string content, IReadOnlyList<MediaAsset> mediaAssets)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ValidateDraftResult(
                new[]
                {
                    new ValidateDraftPostResult(
                        1,
                        0,
                        0,
                        false,
                        new[] { new ValidateDraftIssue(ValidateDraftSeverity.Error, "Blog post content cannot be empty.") })
                },
                ValidateDraftOverallStatus.Failed);
        }

        var issues = new List<ValidateDraftIssue>();

        if (GitHubBlogDocumentBuilder.HasUnclosedFrontmatter(content))
        {
            issues.Add(new ValidateDraftIssue(
                ValidateDraftSeverity.Error,
                "YAML frontmatter starts with '---' but is missing a closing '---' delimiter."));
        }

        var (frontmatterLines, body) = GitHubBlogDocumentBuilder.SplitFrontmatter(content);

        if (string.IsNullOrWhiteSpace(body))
        {
            issues.Add(new ValidateDraftIssue(
                ValidateDraftSeverity.Error,
                "Blog post body cannot be empty."));
        }

        if (PlaceholderLinkRegex.IsMatch(body))
        {
            issues.Add(new ValidateDraftIssue(
                ValidateDraftSeverity.Error,
                "Blog post contains unreplaced placeholder links."));
        }

        var hasFrontmatterTitle = frontmatterLines.Any(line => FrontmatterTitleRegex.IsMatch(line.Trim()));
        var hasLeadingH1 = LeadingH1Regex.IsMatch(body);
        if (!hasFrontmatterTitle && !hasLeadingH1)
        {
            issues.Add(new ValidateDraftIssue(
                ValidateDraftSeverity.Warning,
                "No explicit title found in YAML frontmatter or leading '# H1' heading; the draft title will be used on publish."));
        }

        int imageCount = 0;
        bool hasYouTube = false;
        foreach (Match match in MarkdownImageRegex.Matches(body))
        {
            var alt = match.Groups["alt"].Value;
            var url = match.Groups["url"].Value;

            if (GitHubBlogDocumentBuilder.TryExtractYouTubeVideoId(url, out _))
            {
                hasYouTube = true;
                continue;
            }

            imageCount++;
            if (string.IsNullOrWhiteSpace(alt))
            {
                var missingAlt = true;
                string label = url;
                if (ThreadSegmentDocument.TryExtractMediaId(url, out var mediaId))
                {
                    var asset = mediaAssets?.FirstOrDefault(m => m.Id == mediaId);
                    if (asset is not null)
                    {
                        label = asset.FileName;
                        missingAlt = string.IsNullOrWhiteSpace(asset.AltText);
                    }
                    else
                    {
                        label = mediaId.ToString();
                    }
                }

                if (missingAlt)
                {
                    issues.Add(new ValidateDraftIssue(
                        ValidateDraftSeverity.Warning,
                        $"Missing ALT text on image: {label}."));
                }
            }
        }

        var hasErrors = issues.Any(i => i.Severity == ValidateDraftSeverity.Error);
        var hasWarnings = issues.Any(i => i.Severity == ValidateDraftSeverity.Warning);
        var overallStatus = hasErrors
            ? ValidateDraftOverallStatus.Failed
            : hasWarnings
                ? ValidateDraftOverallStatus.Warnings
                : ValidateDraftOverallStatus.Valid;

        var postResult = new ValidateDraftPostResult(
            1,
            body.Trim().Length,
            imageCount,
            hasYouTube,
            issues);

        return new ValidateDraftResult(new[] { postResult }, overallStatus);
    }
}
