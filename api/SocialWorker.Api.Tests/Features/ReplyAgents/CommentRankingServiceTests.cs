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

public sealed class CommentRankingServiceTests : SqliteTestBase
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

    private static EnrichedCommentContext CreateTestContext(string text)
    {
        var comment = new PlatformCommentItem(
            Platform: "Bluesky",
            RemoteCommentId: "c1",
            RemoteParentId: null,
            RemoteRootId: "root1",
            RemoteAuthorHandle: "alice.bsky.social",
            RemoteAuthorId: "did:alice",
            Text: text,
            CreatedAt: DateTime.UtcNow,
            Depth: 1,
            ExtractedUrls: Array.Empty<string>(),
            ExtractedImages: Array.Empty<PlatformCommentImageAttachment>()
        );

        return new EnrichedCommentContext(
            Comment: comment,
            Links: Array.Empty<ParsedLinkContext>(),
            Images: Array.Empty<ParsedImageContext>(),
            FormattedContextForLlm: $"Comment by @alice.bsky.social: \"{text}\""
        );
    }

    [Fact]
    public async Task RankCommentAsync_NoProvider_ReturnsDefaultSkip()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var providerService = new LlmProviderService();
        var adapter = new MockLlmAdapter(_ => new OpenAiModels.ChatCompletionResponse());

        var service = new CommentRankingService(db, providerService, adapter, NullLogger<CommentRankingService>.Instance);
        var context = CreateTestContext("Why does this happen?");

        var result = await service.RankCommentAsync(context, "Original post", null, user);

        Assert.False(result.ShouldReply);
        Assert.Equal(0, result.Score);
        Assert.Contains("No configured LLM provider", result.Reasoning);
    }

    [Fact]
    public async Task RankCommentAsync_GenuineQuestion_ParsesValidJson()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var provider = LlmProvider.Create("TestProvider", "OpenRouter", "https://api.openai.com/v1", "key", "gpt-4o", isDefault: true);
        db.LlmProviders.Add(provider);
        await db.SaveChangesAsync();

        var jsonResponse = """
        {
          "score": 9,
          "category": "GenuineQuestion",
          "shouldReply": true,
          "isTerminal": false,
          "reasoning": "The user is asking a relevant question about installation steps."
        }
        """;

        var adapter = new MockLlmAdapter(_ => new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = jsonResponse
                    }
                }
            }
        });

        var providerService = new LlmProviderService();
        var service = new CommentRankingService(db, providerService, adapter, NullLogger<CommentRankingService>.Instance);
        var context = CreateTestContext("How do I install the required packages?");

        var result = await service.RankCommentAsync(context, "Original post", null, user);

        Assert.True(result.ShouldReply);
        Assert.Equal(9, result.Score);
        Assert.Equal("GenuineQuestion", result.Category);
        Assert.False(result.IsTerminal);
    }

    [Fact]
    public async Task RankCommentAsync_TerminalClosure_RecognizesClosure()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var provider = LlmProvider.Create("TestProvider", "OpenRouter", "https://api.openai.com/v1", "key", "gpt-4o", isDefault: true);
        db.LlmProviders.Add(provider);
        await db.SaveChangesAsync();

        var jsonResponse = """
        {
          "score": 4,
          "category": "TerminalClosure",
          "shouldReply": false,
          "isTerminal": true,
          "reasoning": "User said 'thanks, got it working!' concluding the interaction."
        }
        """;

        var adapter = new MockLlmAdapter(_ => new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = jsonResponse
                    }
                }
            }
        });

        var providerService = new LlmProviderService();
        var service = new CommentRankingService(db, providerService, adapter, NullLogger<CommentRankingService>.Instance);
        var context = CreateTestContext("Thanks, got it working!");

        var result = await service.RankCommentAsync(context, "Original post", null, user);

        Assert.False(result.ShouldReply);
        Assert.True(result.IsTerminal);
        Assert.Equal("TerminalClosure", result.Category);
    }

    [Fact]
    public async Task RankCommentAsync_MalformedJson_FallsBackToDefaultSkip()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var provider = LlmProvider.Create("TestProvider", "OpenRouter", "https://api.openai.com/v1", "key", "gpt-4o", isDefault: true);
        db.LlmProviders.Add(provider);
        await db.SaveChangesAsync();

        var adapter = new MockLlmAdapter(_ => new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = "I think we shouldn't reply but I forgot JSON format."
                    }
                }
            }
        });

        var providerService = new LlmProviderService();
        var service = new CommentRankingService(db, providerService, adapter, NullLogger<CommentRankingService>.Instance);
        var context = CreateTestContext("Hello");

        var result = await service.RankCommentAsync(context, "Original post", null, user);

        Assert.False(result.ShouldReply);
        Assert.Contains("Failed to parse JSON", result.Reasoning);
    }

    [Fact]
    public async Task RankCommentAsync_CasualPraise_ParsesShouldLike()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var provider = LlmProvider.Create("TestProvider", "OpenRouter", "https://api.openai.com/v1", "key", "gpt-4o", isDefault: true);
        db.LlmProviders.Add(provider);
        await db.SaveChangesAsync();

        var jsonResponse = """
        {
          "score": 5,
          "category": "CasualPraise",
          "shouldReply": false,
          "shouldLike": true,
          "isTerminal": false,
          "reasoning": "Simple compliment, worth liking to show appreciation."
        }
        """;

        var adapter = new MockLlmAdapter(_ => new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = jsonResponse
                    }
                }
            }
        });

        var providerService = new LlmProviderService();
        var service = new CommentRankingService(db, providerService, adapter, NullLogger<CommentRankingService>.Instance);
        var context = CreateTestContext("Awesome post, really liked it!");

        var result = await service.RankCommentAsync(context, "Original post", null, user);

        Assert.False(result.ShouldReply);
        Assert.True(result.ShouldLike);
        Assert.Equal("CasualPraise", result.Category);
        Assert.Equal(5, result.Score);
    }

    [Fact]
    public async Task RankCommentAsync_PoliticalDebate_ScoresHighAndReplies()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var provider = LlmProvider.Create("TestProvider", "OpenRouter", "https://api.openai.com/v1", "key", "gpt-4o", isDefault: true);
        db.LlmProviders.Add(provider);
        await db.SaveChangesAsync();

        var jsonResponse = """
        {
          "score": 8,
          "category": "PoliticalDebate",
          "shouldReply": true,
          "shouldLike": false,
          "isTerminal": false,
          "reasoning": "Partisan challenge and accusation regarding corruption; offers a hook for political debate."
        }
        """;

        var adapter = new MockLlmAdapter(_ => new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = jsonResponse
                    }
                }
            }
        });

        var providerService = new LlmProviderService();
        var service = new CommentRankingService(db, providerService, adapter, NullLogger<CommentRankingService>.Instance);
        var context = CreateTestContext("O pior é vocês que votam sabendo que são corruptos!");

        var result = await service.RankCommentAsync(context, "Original post", null, user);

        Assert.True(result.ShouldReply);
        Assert.False(result.ShouldLike);
        Assert.Equal("PoliticalDebate", result.Category);
        Assert.Equal(8, result.Score);
    }
}
