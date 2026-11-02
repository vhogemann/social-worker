using System;
using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts.Services;
using SocialWorker.Api.Infrastructure.Services;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Drafts;

public class DraftPreviewHtmlRendererTests
{
    private class FakeUrlResolver : IAppUrlResolver
    {
        public string GetWebBaseUrl() => "https://social.plopsaurus.xyz";
        public string GetApiBaseUrl() => "https://social.plopsaurus.xyz/api";
        public string GetMediaUrl(Guid mediaId) => $"https://social.plopsaurus.xyz/api/media/{mediaId}";
        public string GetDraftWebUrl(Guid draftId) => $"https://social.plopsaurus.xyz/draft/{draftId}";
        public string GetDraftPreviewHtmlUrl(Guid draftId, string? platform = null) =>
            $"https://social.plopsaurus.xyz/api/drafts/{draftId}/preview?platform={platform ?? "Bluesky"}";
    }

    [Fact]
    public void Render_WithEmptyDraftContent_FallsBackToThreadContent()
    {
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = "My Fallback Draft",
            Content = null,
            Threads = new List<PlatformThread>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Platform = "Bluesky",
                    Content = "Thread post 1 content\n\n---\n\nThread post 2 content"
                }
            }
        };

        var html = DraftPreviewHtmlRenderer.Render(draft, "Bluesky", new FakeUrlResolver());

        Assert.Contains("Thread post 1 content", html);
        Assert.Contains("Thread post 2 content", html);
        Assert.Contains("Post 1/2", html);
        Assert.Contains("Post 2/2", html);
    }

    [Fact]
    public void Render_ConvertsYouTubeEmbedMarkdown_ToResponsiveIframe()
    {
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = "YouTube Test",
            Content = "Check out this video:\n\n![Rickroll](https://www.youtube.com/watch?v=dQw4w9WgXcQ)"
        };

        var html = DraftPreviewHtmlRenderer.Render(draft, "Bluesky", new FakeUrlResolver());

        Assert.Contains("<iframe", html);
        Assert.Contains("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", html);
        Assert.DoesNotContain("![Rickroll]", html);
    }

    [Fact]
    public void Render_ConvertsBareYouTubeUrl_ToResponsiveIframeAndClickableLink()
    {
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = "Bare YouTube Link",
            Content = "Steve Eisman analyzing Anthropic: https://www.youtube.com/watch?v=8uoSVG3P6z0"
        };

        var html = DraftPreviewHtmlRenderer.Render(draft, "Bluesky", new FakeUrlResolver());

        Assert.Contains("<iframe", html);
        Assert.Contains("https://www.youtube-nocookie.com/embed/8uoSVG3P6z0", html);
        Assert.Contains("href=\"https://www.youtube.com/watch?v=8uoSVG3P6z0\"", html);
    }

    [Fact]
    public void Render_ConvertsMediaEmbed_ToImgTagWithResolvedUrl()
    {
        var mediaId = Guid.NewGuid();
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = "Media Test",
            Content = $"Important quote card:\n\n![Quote Card](media://{mediaId})"
        };

        var html = DraftPreviewHtmlRenderer.Render(draft, "Bluesky", new FakeUrlResolver());

        Assert.Contains("<img", html);
        Assert.Contains($"https://social.plopsaurus.xyz/api/media/{mediaId}", html);
        Assert.DoesNotContain("media://", html);
        Assert.DoesNotContain("![Quote Card]", html);
    }

    [Fact]
    public void Render_IncludesColorSchemeAndHighContrastStyles()
    {
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = "Contrast Test",
            Content = "High contrast visible text"
        };

        var html = DraftPreviewHtmlRenderer.Render(draft, "Bluesky", new FakeUrlResolver());

        Assert.Contains("color-scheme", html);
        Assert.Contains("High contrast visible text", html);
        Assert.Contains("Open Draft in social-worker", html);
    }
}
