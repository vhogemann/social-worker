using System;
using SocialWorker.Api.Features.Sources;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Sources;

public sealed class SourceUrlValidatorTests
{
    private readonly SourceUrlValidator _validator = new();

    [Theory]
    [InlineData("https://example.com/article")]
    [InlineData("https://youtube.com/watch?v=12345")]
    [InlineData("https://github.com/dotnet/runtime")]
    [InlineData("http://blog.cloudflare.com/post")]
    public void EnsureAbsoluteHttpUrl_AllowsValidPublicUrls(string url)
    {
        _validator.EnsureAbsoluteHttpUrl(url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///etc/passwd")]
    public void EnsureAbsoluteHttpUrl_RejectsInvalidOrNonHttpSchemes(string url)
    {
        Assert.Throws<ArgumentException>(() => _validator.EnsureAbsoluteHttpUrl(url));
    }

    [Theory]
    [InlineData("http://localhost/admin")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://0.0.0.0")]
    [InlineData("http://10.0.0.5/internal")]
    [InlineData("http://172.16.0.1")]
    [InlineData("http://172.20.0.2")]
    [InlineData("http://192.168.1.1/router")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://[::1]")]
    [InlineData("http://[fe80::1]")]
    public void EnsureAbsoluteHttpUrl_RejectsPrivateAndLoopbackAddresses_SSRF(string url)
    {
        Assert.Throws<ArgumentException>(() => _validator.EnsureAbsoluteHttpUrl(url));
    }
}
