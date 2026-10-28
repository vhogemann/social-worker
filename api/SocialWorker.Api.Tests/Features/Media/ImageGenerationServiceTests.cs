using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ImageEngine;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Media.Models;
using SocialWorker.Api.Features.Media.Services;
using SocialWorker.Api.Infrastructure.Llm;
using SocialWorker.Api.Tests.Features.ImageEngine;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Media;

public sealed class ImageGenerationServiceTests : SqliteTestBase
{
    private sealed class FakeImageGenerationClient : IImageGenerationClient
    {
        public byte[] ImageBytes { get; set; } = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        public string? LastPrompt { get; private set; }
        public string? LastModel { get; private set; }
        public string? LastAspectRatio { get; private set; }
        public string? LastApiKey { get; private set; }

        public Task<byte[]> GenerateImageAsync(
            string prompt,
            string? model = null,
            string? aspectRatio = "1:1",
            string? apiKey = null,
            CancellationToken ct = default)
        {
            LastPrompt = prompt;
            LastModel = model;
            LastAspectRatio = aspectRatio;
            LastApiKey = apiKey;
            return Task.FromResult(ImageBytes);
        }
    }

    private static (ImageGenerationService Service, FakeImageGenerationClient FakeClient, FakeImageEngineClient FakeEngine) CreateService(
        AppDbContext db,
        string? configApiKey = "test-config-key")
    {
        var fakeClient = new FakeImageGenerationClient();
        var fakeEngine = new FakeImageEngineClient();
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, fakeEngine, storage);
        var providerService = new LlmProviderService();

        var inMemorySettings = new Dictionary<string, string?>();
        if (configApiKey != null)
        {
            inMemorySettings["LLM__ApiKey"] = configApiKey;
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings).Build();
        var logger = NullLogger<ImageGenerationService>.Instance;

        var svc = new ImageGenerationService(
            fakeClient,
            fakeEngine,
            mediaService,
            db,
            providerService,
            config,
            logger);

        return (svc, fakeClient, fakeEngine);
    }

    [Fact]
    public async Task GenerateAndAttachAsync_PureImage_GeneratesAndAttachesMediaAsset()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = new Draft { Id = Guid.NewGuid(), Title = "Test Draft", Content = "Initial content", UserId = user.Id };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var (service, fakeClient, _) = CreateService(db);

        var cmd = new GenerateImageCommand(
            user.Id,
            draft.Id,
            "Cyberpunk cityscape at night",
            AspectRatio: "16:9",
            Model: "test-model");

        var result = await service.GenerateAndAttachAsync(cmd, CancellationToken.None);

        Assert.Equal("Cyberpunk cityscape at night", fakeClient.LastPrompt);
        Assert.Equal("16:9", fakeClient.LastAspectRatio);
        Assert.Equal("test-model", fakeClient.LastModel);
        Assert.NotEqual(Guid.Empty, result.MediaAssetId);
        Assert.StartsWith("![Cyberpunk cityscape at night](media://", result.MarkdownTag);

        var assetInDb = await db.MediaAssets.FindAsync(result.MediaAssetId);
        Assert.NotNull(assetInDb);
        Assert.Equal(draft.Id, assetInDb.DraftId);
    }

    [Fact]
    public async Task GenerateAndAttachAsync_WithTemplate_CompositesImageAndRenders()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = new Draft { Id = Guid.NewGuid(), Title = "Test Draft", Content = "", UserId = user.Id };
        db.Drafts.Add(draft);

        var templateSchema = """
        {
            "width": 1080,
            "height": 1080,
            "slots": [
                { "name": "background_image", "type": "image" },
                { "name": "headline", "type": "text" }
            ],
            "layers": []
        }
        """;

        var template = new Template
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Hero Card",
            Schema = templateSchema,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var (service, _, fakeEngine) = CreateService(db);
        fakeEngine.DummyBytes = new byte[] { 0xAA, 0xBB, 0xCC };

        var cmd = new GenerateImageCommand(
            user.Id,
            draft.Id,
            "Futuristic car",
            TemplateId: template.Id.ToString(),
            TemplateSlots: new Dictionary<string, string> { ["headline"] = "Next Gen Speed" });

        var result = await service.GenerateAndAttachAsync(cmd, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.MediaAssetId);
        var assetInDb = await db.MediaAssets.FindAsync(result.MediaAssetId);
        Assert.NotNull(assetInDb);
    }

    [Fact]
    public async Task GenerateAndAttachAsync_ResolvesApiKeyFromLlmProvider()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = new Draft { Id = Guid.NewGuid(), Title = "Test Draft", Content = "", UserId = user.Id };
        db.Drafts.Add(draft);

        var provider = LlmProvider.Create(
            "OpenRouter Main",
            "OpenRouter",
            "https://openrouter.ai/api/v1",
            "anthropic/claude-3.5-sonnet",
            "sk-provider-key-123",
            isDefault: true);
        db.LlmProviders.Add(provider);
        await db.SaveChangesAsync();

        var (service, fakeClient, _) = CreateService(db, configApiKey: "fallback-key");

        var cmd = new GenerateImageCommand(user.Id, draft.Id, "Test prompt");
        await service.GenerateAndAttachAsync(cmd, CancellationToken.None);

        Assert.Equal("sk-provider-key-123", fakeClient.LastApiKey);
    }
}
