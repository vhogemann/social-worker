using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Chat.Tools;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class FormatValidatePlatformContentToolTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsInvalid_ForUnknownPlatform()
    {
        var tool = new FormatValidatePlatformContentTool(new PlatformContentPolicy());

        var result = await tool.ExecuteAsync(
            new FormatValidatePlatformContentArgs("Mastodon", "Hello world", true),
            null,
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains("Invalid platform", result.Errors.Single());
    }

    [Fact]
    public async Task ExecuteAsync_NormalizesMarkdownStyling_AndFlagsTwitterLength()
    {
        var tool = new FormatValidatePlatformContentTool(new PlatformContentPolicy());
        var tooLong = new string('x', 300);

        var result = await tool.ExecuteAsync(
            new FormatValidatePlatformContentArgs("Twitter", $"**Hook**\n\n{tooLong}", true),
            null,
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.Contains("Hook", result.NormalizedContent);
        Assert.DoesNotContain("**Hook**", result.NormalizedContent, StringComparison.Ordinal);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("exceeds 280 characters", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Warnings, w => w.Contains("Markdown styling", StringComparison.OrdinalIgnoreCase));
    }
}