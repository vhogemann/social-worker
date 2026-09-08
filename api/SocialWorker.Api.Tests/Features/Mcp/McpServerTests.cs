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
using SocialWorker.Api.Features.Scheduling.Services;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Tests.Infrastructure;
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
        var taskQueue = new TestBackgroundTaskQueue();
        var feedPollingService = new FeedPollingService(db, taskQueue, NullLogger<FeedPollingService>.Instance);
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

        var queueService = new ScheduleQueueService(db, taskQueue);
        var evaluatorService = new ScheduleSlotEvaluatorService(db, taskQueue);
        var calendarService = new CalendarProjectionService(db);

        var toolRegistry = new McpToolRegistry(
            db,
            draftsService,
            sourcesService,
            feedPollingService,
            publisherResolver,
            validatorResolver,
            blueskyContentValidator,
            replyResolver,
            queueService,
            evaluatorService,
            calendarService);

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
        Assert.Contains("list_schedules", json);
        Assert.Contains("get_schedule", json);
        Assert.Contains("create_schedule", json);
        Assert.Contains("add_slot_rule", json);
        Assert.Contains("enqueue_draft", json);
        Assert.Contains("get_schedule_queue", json);
        Assert.Contains("get_calendar_events", json);
    }

    [Fact]
    public async Task HandleRequest_ToolsCall_ScheduleTools_Create_AddSlot_Enqueue_GetQueue_Succeeds()
    {
        var (server, user, db) = CreateTestServer();

        // 1. create_schedule
        var createScheduleParams = JsonDocument.Parse("""
        {
            "name": "create_schedule",
            "arguments": {
                "name": "Tech Beat",
                "timezone": "UTC"
            }
        }
        """).RootElement;

        var createScheduleReq = new McpRequest("2.0", 30, "tools/call", createScheduleParams);
        var createScheduleResp = await server.HandleRequestAsync(createScheduleReq, user.Id, CancellationToken.None);
        Assert.NotNull(createScheduleResp);
        var createResult = Assert.IsType<McpToolCallResult>(createScheduleResp.Result);
        Assert.False(createResult.IsError);
        var scheduleDoc = JsonDocument.Parse(createResult.Content[0].Text!);
        var scheduleId = scheduleDoc.RootElement.GetProperty("id").GetString()!;

        // 2. add_slot_rule
        var addSlotParams = JsonDocument.Parse($$"""
        {
            "name": "add_slot_rule",
            "arguments": {
                "scheduleId": "{{scheduleId}}",
                "recurrenceType": "Daily",
                "startTime": "09:00:00",
                "endTime": "10:00:00",
                "postCapacity": 2,
                "daysOfWeek": [1, 2, 3, 4, 5]
            }
        }
        """).RootElement;

        var addSlotReq = new McpRequest("2.0", 31, "tools/call", addSlotParams);
        var addSlotResp = await server.HandleRequestAsync(addSlotReq, user.Id, CancellationToken.None);
        Assert.NotNull(addSlotResp);
        var slotResult = Assert.IsType<McpToolCallResult>(addSlotResp.Result);
        Assert.False(slotResult.IsError);

        // 3. create a draft to enqueue
        var draft = Draft.Create(user.Id, "MCP Test Draft", "Content");
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        // 4. enqueue_draft
        var enqueueParams = JsonDocument.Parse($$"""
        {
            "name": "enqueue_draft",
            "arguments": {
                "scheduleId": "{{scheduleId}}",
                "draftId": "{{draft.Id}}"
            }
        }
        """).RootElement;

        var enqueueReq = new McpRequest("2.0", 32, "tools/call", enqueueParams);
        var enqueueResp = await server.HandleRequestAsync(enqueueReq, user.Id, CancellationToken.None);
        Assert.NotNull(enqueueResp);
        var enqueueResult = Assert.IsType<McpToolCallResult>(enqueueResp.Result);
        Assert.False(enqueueResult.IsError);

        // 5. get_schedule_queue
        var getQueueParams = JsonDocument.Parse($$"""
        {
            "name": "get_schedule_queue",
            "arguments": {
                "scheduleId": "{{scheduleId}}"
            }
        }
        """).RootElement;

        var getQueueReq = new McpRequest("2.0", 33, "tools/call", getQueueParams);
        var getQueueResp = await server.HandleRequestAsync(getQueueReq, user.Id, CancellationToken.None);
        Assert.NotNull(getQueueResp);
        var queueResult = Assert.IsType<McpToolCallResult>(getQueueResp.Result);
        Assert.False(queueResult.IsError);
        Assert.Contains(draft.Id.ToString(), queueResult.Content[0].Text!);

        // 6. get_calendar_events
        var calendarParams = JsonDocument.Parse($$"""
        {
            "name": "get_calendar_events",
            "arguments": {
                "start": "2026-09-01T00:00:00Z",
                "end": "2026-09-07T23:59:59Z",
                "scheduleId": "{{scheduleId}}"
            }
        }
        """).RootElement;

        var calendarReq = new McpRequest("2.0", 34, "tools/call", calendarParams);
        var calendarResp = await server.HandleRequestAsync(calendarReq, user.Id, CancellationToken.None);
        Assert.NotNull(calendarResp);
        var calendarResult = Assert.IsType<McpToolCallResult>(calendarResp.Result);
        Assert.False(calendarResult.IsError);
        Assert.Contains("Tech Beat", calendarResult.Content[0].Text!);
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

    [Fact]
    public async Task HandleRequest_ToolsCall_ListWorkflows_ReturnsDestinationsAndSchedule()
    {
        var (server, user, db) = CreateTestServer();

        var schedule = SocialWorker.Api.Data.Entities.Schedule.Create(user.Id, "Main Social Schedule", "UTC");
        db.Schedules.Add(schedule);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "Bluesky",
            Handle = "science.bsky.social",
            CredentialsEncrypted = "dummy",
            Status = "Active"
        };
        db.Accounts.Add(account);

        var workflow = AutomationWorkflow.Create(user.Id, "PBS Space Time", "Summarize science");
        var dest = AutomationWorkflowDestination.Create(
            workflow.Id,
            "Bluesky",
            WorkflowPublishingMode.Scheduled,
            schedule.Id,
            account.Id);
        workflow.Destinations.Add(dest);
        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        var listParams = JsonDocument.Parse("""
        {
            "name": "list_workflows",
            "arguments": {}
        }
        """).RootElement;

        var request = new McpRequest("2.0", 50, "tools/call", listParams);
        var response = await server.HandleRequestAsync(request, user.Id, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Null(response!.Error);
        var result = Assert.IsType<McpToolCallResult>(response.Result);
        Assert.False(result.IsError);
        Assert.Contains("PBS Space Time", result.Content[0].Text);
        Assert.Contains("Scheduled", result.Content[0].Text);
        Assert.Contains("Main Social Schedule", result.Content[0].Text);
        Assert.Contains("science.bsky.social", result.Content[0].Text);
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



