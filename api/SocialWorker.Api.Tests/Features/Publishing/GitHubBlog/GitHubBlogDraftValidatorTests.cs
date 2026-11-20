using System;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Publishing.GitHubBlog.Validation;
using SocialWorker.Api.Features.Publishing.Validation;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.GitHubBlog;

public sealed class GitHubBlogDraftValidatorTests
{
    private readonly GitHubBlogDraftValidator _validator = new();

    [Fact]
    public void Validate_AllowsLongMarkdownWithHeadingsCodeAndHorizontalRules()
    {
        var content = """
            ---
            title: Deep Dive
            tags: [architecture]
            ---

            ## Introduction

            **Bold** and *italic* text with `code` and a long explanation.

            ```csharp
            Console.WriteLine("Hello");
            ```

            ---

            ## Conclusion

            ![Architecture Diagram](https://example.com/diagram.png)
            """;

        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
        Assert.Single(result.Posts);
        Assert.Empty(result.Posts[0].Issues);
        Assert.Equal(1, result.Posts[0].ImageCount);
    }

    [Fact]
    public void Validate_FailsOnUnclosedFrontmatter()
    {
        var content = "---\ntitle: Broken\n\nBody without closing fence";

        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Error && i.Message.Contains("frontmatter"));
    }

    [Fact]
    public void Validate_WarnsWhenTitleAndAltTextAreMissing()
    {
        var content = "Just a paragraph without a heading.\n\n![](https://example.com/photo.png)";

        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Warnings, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Warning && i.Message.Contains("title"));
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Warning && i.Message.Contains("ALT text"));
    }

    [Fact]
    public void Policy_PreservesMarkdownWhenNormalizing()
    {
        var policy = new PlatformContentPolicy();
        var content = "# Heading\n\n**Bold** and *italic* text.";

        var result = policy.Evaluate(SocialPlatform.GitHubBlog, content, normalizeFormatting: true);

        Assert.True(result.IsValid);
        Assert.Equal(content, result.NormalizedContent);
        Assert.Empty(result.Warnings);
    }
}
