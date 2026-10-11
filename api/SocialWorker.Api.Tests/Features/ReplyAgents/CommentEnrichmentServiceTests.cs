using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Adapters;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Features.ReplyAgents.Services;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Infrastructure.Llm;
using Xunit;

namespace SocialWorker.Api.Tests.Features.ReplyAgents;

public sealed class CommentEnrichmentServiceTests : SqliteTestBase
{
    private sealed class MockLlmAdapter : ILlmProviderAdapter
    {
        private readonly Func<OpenAiModels.ChatCompletionRequest, OpenAiModels.ChatCompletionResponse> _handler;

        public MockLlmAdapter(Func<OpenAiModels.ChatCompletionRequest, OpenAiModels.ChatCompletionResponse> handler)
        {
            _handler = handler;
        }

        public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            CancellationToken ct = default)
        {
            return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(_handler(request));
        }

        public IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Handler { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage>? handler = null)
        {
            if (handler != null) Handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Handler(request));
        }
    }

    [Fact]
    public async Task EnrichCommentAsync_NoUrlsOrImages_ReturnsCleanContext()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var httpHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(httpHandler);
        var scraper = new WebScraperService(httpClient);
        var providerService = new LlmProviderService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var probe = new ModelCapabilityProbe(httpClient, cache, NullLogger<ModelCapabilityProbe>.Instance);
        var adapter = new MockLlmAdapter(_ => new OpenAiModels.ChatCompletionResponse());

        var service = new CommentEnrichmentService(
            scraper, db, providerService, adapter, probe, NullLogger<CommentEnrichmentService>.Instance);

        var comment = new PlatformCommentItem(
            Platform: "Bluesky",
            RemoteCommentId: "c1",
            RemoteParentId: null,
            RemoteRootId: "root1",
            RemoteAuthorHandle: "alice.bsky.social",
            RemoteAuthorId: "did:alice",
            Text: "What do you think about this?",
            CreatedAt: DateTime.UtcNow,
            Depth: 1,
            ExtractedUrls: Array.Empty<string>(),
            ExtractedImages: Array.Empty<PlatformCommentImageAttachment>()
        );

        var enriched = await service.EnrichCommentAsync(comment, user);

        Assert.Empty(enriched.Links);
        Assert.Empty(enriched.Images);
        Assert.Contains("alice.bsky.social", enriched.FormattedContextForLlm);
        Assert.Contains("What do you think about this?", enriched.FormattedContextForLlm);
    }

    [Fact]
    public async Task EnrichCommentAsync_WithUrls_ScrapesAndEnrichesLinks()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var html = "<html><head><title>Tech Breakthrough</title></head><body><p>AI developments in 2026 are accelerating.</p></body></html>";
        var httpHandler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
            };
        });
        var httpClient = new HttpClient(httpHandler);
        var scraper = new WebScraperService(httpClient);
        var providerService = new LlmProviderService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var probe = new ModelCapabilityProbe(httpClient, cache, NullLogger<ModelCapabilityProbe>.Instance);
        var adapter = new MockLlmAdapter(_ => new OpenAiModels.ChatCompletionResponse());

        var service = new CommentEnrichmentService(
            scraper, db, providerService, adapter, probe, NullLogger<CommentEnrichmentService>.Instance);

        var comment = new PlatformCommentItem(
            Platform: "Bluesky",
            RemoteCommentId: "c2",
            RemoteParentId: null,
            RemoteRootId: "root1",
            RemoteAuthorHandle: "bob.bsky.social",
            RemoteAuthorId: "did:bob",
            Text: "Have you seen this? https://example.com/breakthrough",
            CreatedAt: DateTime.UtcNow,
            Depth: 1,
            ExtractedUrls: new[] { "https://example.com/breakthrough" },
            ExtractedImages: Array.Empty<PlatformCommentImageAttachment>()
        );

        var enriched = await service.EnrichCommentAsync(comment, user);

        Assert.Single(enriched.Links);
        Assert.Equal("Tech Breakthrough", enriched.Links[0].Title);
        Assert.True(enriched.Links[0].Success);
        Assert.Contains("AI developments in 2026", enriched.Links[0].Snippet);
        Assert.Contains("Referenced Links:", enriched.FormattedContextForLlm);
    }

    [Fact]
    public async Task EnrichCommentAsync_WithImages_UsesAltTextWhenNoVision()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var httpHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(httpHandler);
        var scraper = new WebScraperService(httpClient);
        var providerService = new LlmProviderService();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var probe = new ModelCapabilityProbe(httpClient, cache, NullLogger<ModelCapabilityProbe>.Instance);
        var adapter = new MockLlmAdapter(_ => new OpenAiModels.ChatCompletionResponse());

        var service = new CommentEnrichmentService(
            scraper, db, providerService, adapter, probe, NullLogger<CommentEnrichmentService>.Instance);

        var comment = new PlatformCommentItem(
            Platform: "Bluesky",
            RemoteCommentId: "c3",
            RemoteParentId: null,
            RemoteRootId: "root1",
            RemoteAuthorHandle: "carol.bsky.social",
            RemoteAuthorId: "did:carol",
            Text: "Look at this screenshot",
            CreatedAt: DateTime.UtcNow,
            Depth: 1,
            ExtractedUrls: Array.Empty<string>(),
            ExtractedImages: new[] { new PlatformCommentImageAttachment("https://cdn.example.com/img.png", "Error: connection timeout") }
        );

        var enriched = await service.EnrichCommentAsync(comment, user);

        Assert.Single(enriched.Images);
        Assert.Equal("Error: connection timeout", enriched.Images[0].AltText);
        Assert.Contains("Error: connection timeout", enriched.Images[0].VisualDescription);
        Assert.Contains("Attached Images:", enriched.FormattedContextForLlm);
    }
}
