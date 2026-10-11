using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Adapters;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Features.ReplyAgents.Services;
using SocialWorker.Api.Infrastructure.Llm;
using Xunit;

namespace SocialWorker.Api.Tests.Features.ReplyAgents;

public sealed class CommentThreadSummarizerTests : SqliteTestBase
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

    [Fact]
    public async Task CompactThreadAsync_EmptyComments_ReturnsExistingSummary()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var providerService = new LlmProviderService();
        var adapter = new MockLlmAdapter(_ => new OpenAiModels.ChatCompletionResponse());
        var summarizer = new CommentThreadSummarizer(db, providerService, adapter, NullLogger<CommentThreadSummarizer>.Instance);

        var res = await summarizer.CompactThreadAsync("Prior summary", Array.Empty<PlatformCommentItem>(), user);

        Assert.Equal("Prior summary", res);
    }

    [Fact]
    public async Task CompactThreadAsync_CallsLlmAndReturnsUpdatedSummary()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var provider = LlmProvider.Create("TestProvider", "OpenRouter", "https://api.openai.com/v1", "key", "gpt-4o", isDefault: true);
        db.LlmProviders.Add(provider);
        await db.SaveChangesAsync();

        var adapter = new MockLlmAdapter(req => new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = "Alice asked about pricing, Bob answered it is open source and free."
                    }
                }
            }
        });

        var providerService = new LlmProviderService();
        var summarizer = new CommentThreadSummarizer(db, providerService, adapter, NullLogger<CommentThreadSummarizer>.Instance);

        var comments = new List<PlatformCommentItem>
        {
            new("Bluesky", "c1", null, "root1", "alice", "u1", "Is it free?", DateTime.UtcNow, 1, Array.Empty<string>(), Array.Empty<PlatformCommentImageAttachment>()),
            new("Bluesky", "c2", "c1", "root1", "bob", "u2", "Yes, completely free!", DateTime.UtcNow, 2, Array.Empty<string>(), Array.Empty<PlatformCommentImageAttachment>())
        };

        var res = await summarizer.CompactThreadAsync(null, comments, user);

        Assert.Equal("Alice asked about pricing, Bob answered it is open source and free.", res);
    }
}
