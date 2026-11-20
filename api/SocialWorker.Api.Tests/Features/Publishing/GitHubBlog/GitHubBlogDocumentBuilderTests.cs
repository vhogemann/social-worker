using System;
using System.Collections.Generic;
using System.Text;
using SocialWorker.Api.Features.Publishing.GitHubBlog;
using SocialWorker.Api.Infrastructure.Platforms;
using SocialWorker.Api.Data;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.GitHubBlog;

public sealed class GitHubBlogDocumentBuilderTests
{
    private readonly GitHubBlogDocumentBuilder _builder = new();

    private static GitHubBlogCredentials CreateCredentials(
        string? siteBaseUrl = "https://blog.example.com",
        string? permalinkFormat = "/{year}/{month}/{day}/{slug}/",
        string? author = null,
        List<string>? defaultCategories = null) =>
        new(
            Owner: "alice",
            Repo: "alice.github.io",
            Branch: "main",
            AccessToken: "ghp_test",
            Preset: "jekyll",
            PostsDirectory: "_posts",
            AssetsDirectory: "assets/images/posts",
            SiteBaseUrl: siteBaseUrl,
            PermalinkFormat: permalinkFormat,
            DefaultLayout: "post",
            Author: author,
            DefaultCategories: defaultCategories);

    [Fact]
    public void Build_PromotesLeadingH1AndStripsItFromBody()
    {
        var creds = CreateCredentials(author: "Alice", defaultCategories: new List<string> { "engineering" });
        var date = new DateTime(2026, 11, 19, 14, 30, 0, DateTimeKind.Utc);
        var markdown = "# Hello Café World!\n\nThis is the **intro** paragraph.\n\n## Section 2\n\nMore details.";

        var result = _builder.Build(markdown, "Fallback Title", creds, new Dictionary<Guid, GitHubBlogMediaAssetInput>(), date);

        Assert.Equal("Hello Café World!", result.Title);
        Assert.Equal("hello-cafe-world", result.Slug);
        Assert.Equal("_posts/2026-11-19-hello-cafe-world.md", result.PostPath);
        Assert.Equal("https://blog.example.com/2026/11/19/hello-cafe-world/", result.PostUrl);
        Assert.Contains("layout: post", result.FinalMarkdown);
        Assert.Contains("title: \"Hello Café World!\"", result.FinalMarkdown);
        Assert.Contains("date: 2026-11-19 14:30:00 +0000", result.FinalMarkdown);
        Assert.Contains("author: \"Alice\"", result.FinalMarkdown);
        Assert.Contains("categories: [\"engineering\"]", result.FinalMarkdown);
        Assert.DoesNotContain("# Hello Café World!", result.FinalMarkdown);
        Assert.Contains("This is the **intro** paragraph.", result.FinalMarkdown);
        Assert.Contains("## Section 2", result.FinalMarkdown);
    }

    [Fact]
    public void Build_MergesExistingYamlFrontmatterWithoutClobberingCustomFields()
    {
        var creds = CreateCredentials(permalinkFormat: "/posts/{slug}/");
        var date = new DateTime(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc);
        var markdown = """
            ---
            title: Custom Title
            tags: [dotnet, jekyll]
            pin: true
            ---

            Body text with `inline code` and a divider.

            ---

            Second section.
            """;

        var result = _builder.Build(markdown, "Ignored Fallback", creds, new Dictionary<Guid, GitHubBlogMediaAssetInput>(), date);

        Assert.Equal("Custom Title", result.Title);
        Assert.Equal("custom-title", result.Slug);
        Assert.Equal("https://blog.example.com/posts/custom-title/", result.PostUrl);
        Assert.Contains("layout: post", result.FinalMarkdown);
        Assert.Contains("title: Custom Title", result.FinalMarkdown);
        Assert.Contains("tags: [dotnet, jekyll]", result.FinalMarkdown);
        Assert.Contains("pin: true", result.FinalMarkdown);
        Assert.Contains("---\n\nSecond section.", result.FinalMarkdown);
    }

    [Fact]
    public void Build_RewritesLocalMediaPathsAndAutoSetsFrontmatterImage()
    {
        var creds = CreateCredentials(siteBaseUrl: null);
        var date = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
        var mediaId1 = Guid.NewGuid();
        var mediaId2 = Guid.NewGuid();
        var media = new Dictionary<Guid, GitHubBlogMediaAssetInput>
        {
            [mediaId1] = new(mediaId1, "Hero Banner.PNG", Encoding.UTF8.GetBytes("img1")),
            [mediaId2] = new(mediaId2, "diagram.webp", Encoding.UTF8.GetBytes("img2"))
        };

        var markdown = $"# Visual Post\n\n![Hero](/api/media/{mediaId1})\n\n![Diagram](media://{mediaId2})\n\n![External](https://cdn.example.com/pic.jpg)";

        var result = _builder.Build(markdown, null, creds, media, date);

        Assert.Equal(2, result.MediaUploads.Count);
        Assert.Equal("assets/images/posts/2026-11-19-visual-post/hero-banner.png", result.MediaUploads[0].RepoPath);
        Assert.Equal("assets/images/posts/2026-11-19-visual-post/diagram.webp", result.MediaUploads[1].RepoPath);
        Assert.Contains("image: /assets/images/posts/2026-11-19-visual-post/hero-banner.png", result.FinalMarkdown);
        Assert.Contains("![Hero](/assets/images/posts/2026-11-19-visual-post/hero-banner.png)", result.FinalMarkdown);
        Assert.Contains("![Diagram](/assets/images/posts/2026-11-19-visual-post/diagram.webp)", result.FinalMarkdown);
        Assert.Contains("![External](https://cdn.example.com/pic.jpg)", result.FinalMarkdown);
        Assert.Equal("https://github.com/alice/alice.github.io/blob/main/_posts/2026-11-19-visual-post.md", result.PostUrl);
    }

    [Fact]
    public void Build_ConvertsYouTubeImageEmbedsToClickableThumbnailLinks()
    {
        var creds = CreateCredentials();
        var date = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
        var markdown = "# Video Post\n\n![Demo Video](https://www.youtube.com/watch?v=dQw4w9WgXcQ)";

        var result = _builder.Build(markdown, null, creds, new Dictionary<Guid, GitHubBlogMediaAssetInput>(), date);

        Assert.Contains("[![Demo Video](https://img.youtube.com/vi/dQw4w9WgXcQ/hqdefault.jpg)](https://www.youtube.com/watch?v=dQw4w9WgXcQ)", result.FinalMarkdown);
    }

    [Fact]
    public void Build_AppliesCollisionSuffixToPostAndAssetPaths()
    {
        var creds = CreateCredentials();
        var date = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
        var result = _builder.Build("# Repeated Title\n\nHello", null, creds, new Dictionary<Guid, GitHubBlogMediaAssetInput>(), date, slugSuffix: "2");

        Assert.Equal("repeated-title-2", result.Slug);
        Assert.Equal("_posts/2026-11-19-repeated-title-2.md", result.PostPath);
        Assert.Equal("https://blog.example.com/2026/11/19/repeated-title-2/", result.PostUrl);
    }

    [Fact]
    public void Build_WithDocsSiteRootDirectory_PrefixesRepoPathsButKeepsWebRootRelativeImageUrls()
    {
        var creds = new GitHubBlogCredentials(
            Owner: "alice",
            Repo: "alice.github.io",
            Branch: "main",
            AccessToken: "ghp_test",
            SiteRootDirectory: "docs",
            SiteBaseUrl: "https://alice.github.io");
        var date = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
        var mediaId = Guid.NewGuid();
        var media = new Dictionary<Guid, GitHubBlogMediaAssetInput>
        {
            [mediaId] = new(mediaId, "diagram.png", Encoding.UTF8.GetBytes("png"))
        };

        var result = _builder.Build($"# Docs Root Post\n\n![Diagram](media://{mediaId})", null, creds, media, date);

        Assert.Equal("docs/_posts/2026-11-19-docs-root-post.md", result.PostPath);
        Assert.Single(result.MediaUploads);
        Assert.Equal("docs/assets/images/posts/2026-11-19-docs-root-post/diagram.png", result.MediaUploads[0].RepoPath);
        Assert.Contains("image: /assets/images/posts/2026-11-19-docs-root-post/diagram.png", result.FinalMarkdown);
        Assert.Contains("![Diagram](/assets/images/posts/2026-11-19-docs-root-post/diagram.png)", result.FinalMarkdown);
        Assert.DoesNotContain("/docs/assets/", result.FinalMarkdown);
    }

    [Theory]
    [InlineData("GitHubBlog")]
    [InlineData("github-blog")]
    [InlineData("github_blog")]
    [InlineData("jekyll")]
    [InlineData("blog")]
    public void SocialPlatformParser_ParsesGitHubBlogAliases(string alias)
    {
        Assert.True(SocialPlatformParser.TryParse(alias, out var parsed));
        Assert.Equal(SocialPlatform.GitHubBlog, parsed);
        Assert.Equal("GitHub Blog", SocialPlatformParser.GetDisplayName(parsed));
    }
}
