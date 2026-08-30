using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Mcp.Models;
using SocialWorker.Api.Features.Mcp.Services;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Features.Sources;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Mcp;

public sealed class McpServerTests : SqliteTestBase
{
    private (McpServer Server, AppUser User, AppDbContext Db) CreateTestServer()
    {
        var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var sourcesService = TestServiceFactory.CreateSourcesService(db);
        var storage = new FileStorageProvider(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var draftsService = TestServiceFactory.CreateDraftsService(db, storage, sourcesService);
        var feedPollingService = new FeedPollingService(db, new TestBackgroundTaskQueue(), NullLogger<FeedPollingService>.Instance);
        var publisherResolver = new MockPublisherResolver();
        var blueskyContentValidator = new BlueskyContentValidator();
        var replyResolver = new MockBlueskyReplyTargetResolver();
        var blueskyDraftValidator = new BlueskyDraftValidator(
            blueskyContentValidator,
            new IValidateDraftRule<BlueskySegmentValidation>[]
            {
                new BlueskyMaxCharactersRule(),
                new BlueskyMaxImagesRule()
            });
        var validatorResolver = new DraftValidatorResolver(
            new CanonicalDraftValidator(),
            blueskyDraftValidator,
            new SocialWorker.Api.Features.Publishing.LinkedIn.LinkedInDraftValidator(),
            new PlatformContentPolicy());

        var toolRegistry = new McpToolRegistry(
            db,
            draftsService,
            sourcesService,
            feedPollingService,
            publisherResolver,
            validatorResolver,
            blueskyContentValidator,
            replyResolver);

        var resourceRegistry = new McpResourceRegistry(db, draftsService);
        var server = new McpServer(toolRegistry, resourceRegistry);

        return (server, user, db);
    }

    [Fact]
    public async Task HandleRequest_Initialize_ReturnsProtocolVersionAndCapabilities()
    {
        var (server, user, _) = CreateTestServer();
        var request = new McpRequest("2.0", 1, "initialize", null);

        var response = await server.HandleRequestAsync(request, user.Id, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Null(response!.Error);
        var result = Assert.IsType<McpInitializeResult>(response.Result);
        Assert.Equal("2024-11-05", result.ProtocolVersion);
        Assert.Equal("SocialWorker", result.ServerInfo.Name);
        Assert.NotNull(result.Capabilities.Tools);
        Assert.NotNull(result.Capabilities.Resources);
    }

    [Fact]
    public async Task HandleRequest_ToolsList_ReturnsAllDomainTools()
    {
        var (server, user, _) = CreateTestServer();
        var request = new McpRequest("2.0", 2, "tools/list", null);

        var response = await server.HandleRequestAsync(request, user.Id, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Null(response!.Error);
        var json = JsonSerializer.Serialize(response.Result);
        Assert.Contains("list_drafts", json);
        Assert.Contains("create_draft", json);
        Assert.Contains("validate_draft", json);
        Assert.Contains("publish_thread", json);
        Assert.Contains("list_sources", json);
        Assert.Contains("add_source", json);
        Assert.Contains("list_workflows", json);
        Assert.Contains("list_feeds", json);
    }

    [Fact]
    public async Task HandleRequest_ToolsCall_CreateDraft_Succeeds()
    {
        var (server, user, db) = CreateTestServer();
        var paramsJson = JsonDocument.Parse("""
        {
            "name": "create_draft",
            "arguments": {
                "title": "Quantum Computing Breakthrough",
                "content": "First segment.\n\n---\n\nSecond segment.",
                "platform": "Bluesky"
            }
        }
        """).RootElement;

        var request = new McpRequest("2.0", 3, "tools/call", paramsJson);
        var response = await server.HandleRequestAsync(request, user.Id, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Null(response!.Error);
        var result = Assert.IsType<McpToolCallResult>(response.Result);
        Assert.False(result.IsError);
        Assert.Contains("Draft created successfully", result.Content[0].Text);

        var drafts = db.Drafts.ToList();
        Assert.Single(drafts);
        Assert.Equal("Quantum Computing Breakthrough", drafts[0].Title);
    }

    [Fact]
    public async Task HandleRequest_ResourcesList_ReturnsFeedsAndWorkflows()
    {
        var (server, user, _) = CreateTestServer();
        var request = new McpRequest("2.0", 4, "resources/list", null);

        var response = await server.HandleRequestAsync(request, user.Id, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Null(response!.Error);
        var json = JsonSerializer.Serialize(response.Result);
        Assert.Contains("socialworker://feeds", json);
        Assert.Contains("socialworker://workflows", json);
    }

    [Fact]
    public async Task HandleRequest_UnknownMethod_ReturnsError()
    {
        var (server, user, _) = CreateTestServer();
        var request = new McpRequest("2.0", 5, "unknown/method", null);

        var response = await server.HandleRequestAsync(request, user.Id, CancellationToken.None);

        Assert.NotNull(response);
        Assert.NotNull(response!.Error);
        Assert.Equal(-32601, response.Error!.Code);
    }

    private sealed class MockPublisherResolver : IPublisherResolver
    {
        public IPublisher? Resolve(string platform) => null;
    }

    private sealed class MockBlueskyReplyTargetResolver : IBlueskyReplyTargetResolver
    {
        public Task<BlueskyReplyTargetResolutionResult> ResolveAsync(string url, CancellationToken ct) =>
            Task.FromResult(new BlueskyReplyTargetResolutionResult(false, null, null, null, null, null, null, null, null, "Not found"));

        public Task<string?> ResolveThreadContextAsync(string url, CancellationToken ct) =>
            Task.FromResult<string?>(null);
    }
}



