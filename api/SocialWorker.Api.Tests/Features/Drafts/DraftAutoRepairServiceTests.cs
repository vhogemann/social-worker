using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Adapters;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Drafts.Services;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure.Llm;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Drafts;

public sealed class DraftAutoRepairServiceTests : SqliteTestBase
{
    private sealed class FakeLlmAdapter : ILlmProviderAdapter
    {
        public string ResponseToReturn { get; set; } = "Short repaired text that passes validation.";
        public int CallCount { get; private set; }
        public OpenAiModels.ChatCompletionRequest? LastRequest { get; private set; }

        public IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            CancellationToken ct) => throw new NotImplementedException();

        public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            CancellationToken ct)
        {
            CallCount++;
            LastRequest = request;
            var response = new OpenAiModels.ChatCompletionResponse
            {
                Choices = new List<OpenAiModels.ChatCompletionChoice>
                {
                    new()
                    {
                        Message = new OpenAiModels.ChatCompletionMessage
                        {
                            Role = "assistant",
                            Content = ResponseToReturn
                        }
                    }
                }
            };
            return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(response);
        }
    }

    private static DraftValidatorResolver CreateResolver()
    {
        var blueskyValidator = new BlueskyDraftValidator(
            new BlueskyContentValidator(),
            Array.Empty<IValidateDraftRule<BlueskySegmentValidation>>());
        return new DraftValidatorResolver(
            new CanonicalDraftValidator(),
            blueskyValidator,
            new LinkedInDraftValidator(),
            new PlatformContentPolicy());
    }

    [Fact]
    public async Task RepairPlatformThreadAsync_CallsLlmAndUpdatesThreadAndSegments_WhenValid()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "Test Provider",
            ProviderType = "OpenRouter",
            BaseUrl = "https://example.com/v1",
            ApiKey = "key",
            Model = "gpt-4",
            IsDefault = true,
            IsActive = true
        };
        db.LlmProviders.Add(provider);

        var draft = Draft.Create(user.Id, "Test Draft", new string('x', 600));
        db.Drafts.Add(draft);
        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "MetaThreads",
            Stage = PlatformThreadStage.Scheduled,
            Content = draft.Content
        };
        db.PlatformThreads.Add(thread);
        await db.SaveChangesAsync();

        var fakeAdapter = new FakeLlmAdapter
        {
            ResponseToReturn = "First repaired segment under limit.\n\n---\n\nSecond repaired segment under limit."
        };
        var policy = new PlatformContentPolicy();
        var resolver = CreateResolver();

        var service = new DraftAutoRepairService(
            db,
            fakeAdapter,
            new LlmProviderService(),
            policy,
            resolver,
            NullLogger<DraftAutoRepairService>.Instance);

        var result = await service.RepairPlatformThreadAsync(
            thread,
            "Post exceeds the 500-character Threads limit by 100 characters.",
            user.Id,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, fakeAdapter.CallCount);
        Assert.Equal(fakeAdapter.ResponseToReturn, thread.Content);
        Assert.Equal(fakeAdapter.ResponseToReturn, draft.Content);

        var segments = await db.ThreadSegments.Where(s => s.DraftId == draft.Id).OrderBy(s => s.Position).ToListAsync();
        Assert.Equal(2, segments.Count);
        Assert.Equal("First repaired segment under limit.", segments[0].Content);
        Assert.Equal("Second repaired segment under limit.", segments[1].Content);
    }

    [Fact]
    public async Task RepairPlatformThreadAsync_Fails_WhenRepairedTextStillExceedsLimits()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "Test Provider",
            ProviderType = "OpenRouter",
            BaseUrl = "https://example.com/v1",
            ApiKey = "key",
            Model = "gpt-4",
            IsDefault = true,
            IsActive = true
        };
        db.LlmProviders.Add(provider);

        var draft = Draft.Create(user.Id, "Test Draft", new string('x', 600));
        db.Drafts.Add(draft);
        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "MetaThreads",
            Stage = PlatformThreadStage.Scheduled,
            Content = draft.Content
        };
        db.PlatformThreads.Add(thread);
        await db.SaveChangesAsync();

        var fakeAdapter = new FakeLlmAdapter
        {
            ResponseToReturn = new string('y', 550)
        };
        var policy = new PlatformContentPolicy();
        var resolver = CreateResolver();

        var service = new DraftAutoRepairService(
            db,
            fakeAdapter,
            new LlmProviderService(),
            policy,
            resolver,
            NullLogger<DraftAutoRepairService>.Instance);

        var result = await service.RepairPlatformThreadAsync(
            thread,
            "Post exceeds the 500-character Threads limit by 100 characters.",
            user.Id,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(fakeAdapter.CallCount >= 2);
    }

    [Fact]
    public async Task RepairDraftAsync_UpdatesDraftAndThreadWhenValid()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "Test Provider",
            ProviderType = "OpenRouter",
            BaseUrl = "https://example.com/v1",
            ApiKey = "key",
            Model = "gpt-4",
            IsDefault = true,
            IsActive = true
        };
        db.LlmProviders.Add(provider);

        var draft = Draft.Create(user.Id, "Draft to repair", new string('z', 700));
        db.Drafts.Add(draft);
        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "MetaThreads",
            Stage = PlatformThreadStage.Draft,
            Content = draft.Content
        };
        db.PlatformThreads.Add(thread);
        await db.SaveChangesAsync();

        var fakeAdapter = new FakeLlmAdapter
        {
            ResponseToReturn = "Short revised text."
        };
        var policy = new PlatformContentPolicy();
        var resolver = CreateResolver();

        var service = new DraftAutoRepairService(
            db,
            fakeAdapter,
            new LlmProviderService(),
            policy,
            resolver,
            NullLogger<DraftAutoRepairService>.Instance);

        var result = await service.RepairDraftAsync(
            draft,
            "MetaThreads",
            "Exceeded 500 characters",
            user.Id,
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Short revised text.", draft.Content);
        Assert.Equal("Short revised text.", thread.Content);
    }
}
