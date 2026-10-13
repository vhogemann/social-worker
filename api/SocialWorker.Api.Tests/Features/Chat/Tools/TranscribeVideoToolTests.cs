using System;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Sources;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat.Tools;

public sealed class TranscribeVideoToolTests
{
    private sealed class MockTranscriber : ITranscriptExtractionService
    {
        public bool ShouldSucceed { get; set; } = true;
        public string? ExtractedUrl { get; private set; }
        public string TranscriptText { get; set; } = "Sample spoken transcript from video";
        public double Duration { get; set; } = 42.5;
        public string Language { get; set; } = "pt";

        public Task<bool> HealthAsync(CancellationToken ct) => Task.FromResult(true);

        public Task<TranscriptExtractionResult> ExtractAsync(string videoUrl, string outputPath, CancellationToken ct)
        {
            ExtractedUrl = videoUrl;
            if (!ShouldSucceed)
            {
                return Task.FromResult(new TranscriptExtractionResult(false, null, null, null, "Failed to download video."));
            }

            return Task.FromResult(new TranscriptExtractionResult(true, outputPath, Duration, Language, null));
        }

        public Task<TranscriptDocument?> ReadTranscriptAsync(string transcriptPath, CancellationToken ct)
        {
            if (!ShouldSucceed) return Task.FromResult<TranscriptDocument?>(null);
            return Task.FromResult<TranscriptDocument?>(new TranscriptDocument(ExtractedUrl, Language, Duration, TranscriptText));
        }
    }

    [Fact]
    public async Task ExecuteAsync_ValidUrl_ReturnsTranscript()
    {
        var transcriber = new MockTranscriber();
        var tool = new TranscribeVideoTool(transcriber);

        var context = ToolExecutionContext.Create(Guid.NewGuid());
        var result = await tool.ExecuteAsync(new TranscribeVideoArgs("https://www.youtube.com/watch?v=12345"), context);

        Assert.True(result.Success);
        Assert.Equal("https://www.youtube.com/watch?v=12345", transcriber.ExtractedUrl);
        Assert.Equal("Sample spoken transcript from video", result.Transcript);
        Assert.Equal("pt", result.Language);
        Assert.Contains("Sample spoken transcript", result.ToDisplayText());
    }

    [Fact]
    public async Task ExecuteAsync_EmptyUrl_ReturnsFailure()
    {
        var transcriber = new MockTranscriber();
        var tool = new TranscribeVideoTool(transcriber);

        var context = ToolExecutionContext.Create(Guid.NewGuid());
        var result = await tool.ExecuteAsync(new TranscribeVideoArgs(""), context);

        Assert.False(result.Success);
        Assert.Contains("No video URL provided", result.Error);
    }

    [Fact]
    public async Task ExecuteAsync_ExtractionFails_ReturnsError()
    {
        var transcriber = new MockTranscriber { ShouldSucceed = false };
        var tool = new TranscribeVideoTool(transcriber);

        var context = ToolExecutionContext.Create(Guid.NewGuid());
        var result = await tool.ExecuteAsync(new TranscribeVideoArgs("https://www.youtube.com/watch?v=bad"), context);

        Assert.False(result.Success);
        Assert.Contains("Failed to download video", result.Error);
    }
}
