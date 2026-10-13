using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Adapters;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Workflows.Services;
using SocialWorker.Api.Infrastructure.Llm;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Workflows;

public sealed class SourceRelevanceEvaluatorTests : SqliteTestBase
{
    private sealed class FakeLlmAdapter : ILlmProviderAdapter
    {
        private readonly string _jsonContent;

        public FakeLlmAdapter(string jsonContent)
        {
            _jsonContent = jsonContent;
        }

        public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield break;
        }

        public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            CancellationToken ct)
        {
            return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(new OpenAiModels.ChatCompletionResponse
            {
                Choices = new List<OpenAiModels.ChatCompletionChoice>
                {
                    new()
                    {
                        Message = new OpenAiModels.ChatCompletionMessage
                        {
                            Role = "assistant",
                            Content = _jsonContent
                        }
                    }
                }
            });
        }
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsPass_WhenMinRelevanceScoreIsZero()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var workflow = AutomationWorkflow.Create(user.Id, "No Filter Workflow", "Summarize");
        workflow.MinRelevanceScore = 0;
        var draft = new Draft { Id = Guid.NewGuid(), UserId = user.Id, Title = "Any News", Status = DraftStatus.Editing };

        var evaluator = new SourceRelevanceEvaluator(
            db,
            new LlmProviderService(),
            new FakeLlmAdapter("{}"),
            NullLogger<SourceRelevanceEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(draft, workflow);

        Assert.True(result.ShouldPublish);
        Assert.Equal(10, result.Score);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsPass_WhenScoreMeetsOrExceedsThreshold()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "OpenRouter",
            ProviderType = "OpenRouter",
            BaseUrl = "https://example.com",
            ApiKey = "key",
            Model = "gpt-4",
            IsDefault = true,
            IsActive = true
        };
        db.LlmProviders.Add(provider);

        var workflow = AutomationWorkflow.Create(user.Id, "Strict Workflow", "Summarize");
        workflow.MinRelevanceScore = 7;
        var draft = new Draft { Id = Guid.NewGuid(), UserId = user.Id, Title = "Major political scandal revealed", Status = DraftStatus.Editing };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var llmJson = """
        {
            "score": 9,
            "shouldPublish": true,
            "reasoning": "Impacto político altíssimo e revelação inédita.",
            "category": "HighImpact"
        }
        """;

        var evaluator = new SourceRelevanceEvaluator(
            db,
            new LlmProviderService(),
            new FakeLlmAdapter(llmJson),
            NullLogger<SourceRelevanceEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(draft, workflow);

        Assert.True(result.ShouldPublish);
        Assert.Equal(9, result.Score);
        Assert.Equal("Impacto político altíssimo e revelação inédita.", result.Reasoning);
        Assert.Equal("HighImpact", result.Category);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsDiscard_WhenScoreBelowThreshold()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "OpenRouter",
            ProviderType = "OpenRouter",
            BaseUrl = "https://example.com",
            ApiKey = "key",
            Model = "gpt-4",
            IsDefault = true,
            IsActive = true
        };
        db.LlmProviders.Add(provider);

        var workflow = AutomationWorkflow.Create(user.Id, "Strict Workflow", "Summarize");
        workflow.MinRelevanceScore = 7;
        var draft = new Draft { Id = Guid.NewGuid(), UserId = user.Id, Title = "Edital de concurso municipal é homologado", Status = DraftStatus.Editing };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var llmJson = """
        {
            "score": 3,
            "shouldPublish": false,
            "reasoning": "Aviso administrativo corriqueiro sem impacto popular.",
            "category": "RoutineFiller"
        }
        """;

        var evaluator = new SourceRelevanceEvaluator(
            db,
            new LlmProviderService(),
            new FakeLlmAdapter(llmJson),
            NullLogger<SourceRelevanceEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(draft, workflow);

        Assert.False(result.ShouldPublish);
        Assert.Equal(3, result.Score);
        Assert.Equal("Aviso administrativo corriqueiro sem impacto popular.", result.Reasoning);
    }

    [Fact]
    public async Task FeedOrchestration_DiscardsDraft_WhenRelevanceScoreBelowWorkflowThreshold()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var workflow = AutomationWorkflow.Create(user.Id, "High Impact Threads", "Focus on working class");
        workflow.MinRelevanceScore = 7;
        db.AutomationWorkflows.Add(workflow);

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Minor procedural update",
            Status = DraftStatus.Editing
        };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var fakeEvaluator = new FakeRelevanceEvaluator(SourceRelevanceResult.Discard(4, "Pouco relevante para debate público"));

        var orchestrationService = new FeedOrchestrationService(
            db: db,
            sourceIngestionService: null!,
            threadGenerationService: null!,
            validationService: null!,
            autoPublishService: null!,
            logger: NullLogger<FeedOrchestrationService>.Instance,
            repairService: null,
            relevanceEvaluator: fakeEvaluator);

        await orchestrationService.ProcessCanonicalDraftOrchestrationAsync(
            draft,
            workflow,
            itemLink: "https://example.com/news",
            userId: user.Id,
            ct: CancellationToken.None);

        var reloaded = await db.Drafts.FindAsync(draft.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(DraftStatus.Discarded, reloaded.Status);
        Assert.Equal(4, reloaded.RelevanceScore);
        Assert.Equal("Pouco relevante para debate público", reloaded.DiscardReason);
        Assert.Contains("Descartado - Relevância 4/10", reloaded.Content);
    }

    private sealed class FakeRelevanceEvaluator : ISourceRelevanceEvaluator
    {
        private readonly SourceRelevanceResult _result;

        public FakeRelevanceEvaluator(SourceRelevanceResult result)
        {
            _result = result;
        }

        public Task<SourceRelevanceResult> EvaluateAsync(Draft draft, AutomationWorkflow workflow, CancellationToken ct = default)
        {
            return Task.FromResult(_result);
        }
    }
}
