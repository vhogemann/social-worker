using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Features.ReplyAgents.Services;
using SocialWorker.Api.Features.Scheduling.Services;
using SocialWorker.Api.Tests.Features.ImageEngine;
using SocialWorker.Api.Tests.Infrastructure;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Mcp;

public sealed class McpServerTests : SqliteTestBase
{
    private (McpServer Server, AppUser User, AppDbContext Db, FileStorageProvider Storage) CreateTestServerWithStorage(
        TestBackgroundTaskQueue? customQueue = null,
        IReplyPlatformAdapterResolver? customAdapterResolver = null)
    {
        var storage = new FileStorageProvider(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var (server, user, db) = CreateTestServer(customQueue, customAdapterResolver, storage);
        return (server, user, db, storage);
    }

    private (McpServer Server, AppUser User, AppDbContext Db) CreateTestServer(
        TestBackgroundTaskQueue? customQueue = null,
        IReplyPlatformAdapterResolver? customAdapterResolver = null,
        FileStorageProvider? customStorage = null,
        IPublisherResolver? customPublisherResolver = null)
    {
        var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var sourcesService = TestServiceFactory.CreateSourcesService(db);
        var storage = customStorage ?? new FileStorageProvider(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var draftsService = TestServiceFactory.CreateDraftsService(db, storage, sourcesService);
        var taskQueue = customQueue ?? new TestBackgroundTaskQueue();
        var feedPollingService = new FeedPollingService(db, taskQueue, NullLogger<FeedPollingService>.Instance);
        var publisherResolver = customPublisherResolver ?? new MockPublisherResolver();
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
        var feedDiscoveryService = new FeedDiscoveryService(new HttpClient());
        var defaultAdapterResolver = customAdapterResolver ?? new MockReplyAdapterResolver(new MockReplyPlatformAdapter());
        var postResolver = new ExternalPostResolver(db, replyResolver, NullLogger<ExternalPostResolver>.Instance);
        var postService = new ReplyAgentPostService(db, postResolver, taskQueue);
        var imageEngine = new FakeImageEngineClient();
        var mediaService = new MediaService(db, imageEngine, storage);

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
            calendarService,
            feedDiscoveryService: feedDiscoveryService,
            taskQueue: taskQueue,
            replyAdapterResolver: defaultAdapterResolver,
            postService: postService,
            mediaService: mediaService,
            imageEngineClient: imageEngine);

        var resourceRegistry = new McpResourceRegistry(db, draftsService, mediaService);
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
        Assert.Contains("list_accounts", json);
        Assert.Contains("discover_feed", json);
        Assert.Contains("add_feed", json);
        Assert.Contains("update_feed", json);
        Assert.Contains("create_workflow", json);
        Assert.Contains("delete_workflow", json);
        Assert.Contains("get_reply_agent", json);
        Assert.Contains("configure_reply_agent", json);
        Assert.Contains("trigger_reply_check", json);
        Assert.Contains("list_comment_audits", json);
        Assert.Contains("reply_to_comment", json);
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

        // 7. delete_slot_rule (specific)
        var slotDoc = JsonDocument.Parse(slotResult.Content[0].Text!);
        var slotRuleId = slotDoc.RootElement.GetProperty("id").GetString()!;
        var deleteSlotParams = JsonDocument.Parse($$"""
        {
            "name": "delete_slot_rule",
            "arguments": {
                "scheduleId": "{{scheduleId}}",
                "slotRuleId": "{{slotRuleId}}"
            }
        }
        """).RootElement;
        var deleteSlotReq = new McpRequest("2.0", 35, "tools/call", deleteSlotParams);
        var deleteSlotResp = await server.HandleRequestAsync(deleteSlotReq, user.Id, CancellationToken.None);
        Assert.NotNull(deleteSlotResp);
        var deleteSlotResult = Assert.IsType<McpToolCallResult>(deleteSlotResp.Result);
        Assert.False(deleteSlotResult.IsError);
        Assert.Contains(slotRuleId, deleteSlotResult.Content[0].Text!);
        Assert.Empty(db.ScheduleSlotRules.Where(r => r.ScheduleId == Guid.Parse(scheduleId)));
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

    [Fact]
    public async Task HandleRequest_ToolsCall_ListAccounts_ReturnsAccountsWithoutSecrets()
    {
        var (server, user, db) = CreateTestServer();
        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "MetaThreads",
            Handle = "vhogemann",
            CredentialsEncrypted = "secret-super-sensitive-token",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        // 1. All accounts
        var allReq = new McpRequest("2.0", 60, "tools/call", JsonDocument.Parse("""
        {
            "name": "list_accounts",
            "arguments": {}
        }
        """).RootElement);
        var allResp = await server.HandleRequestAsync(allReq, user.Id, CancellationToken.None);
        Assert.NotNull(allResp);
        var allResult = Assert.IsType<McpToolCallResult>(allResp.Result);
        Assert.False(allResult.IsError);
        Assert.Contains("vhogemann", allResult.Content[0].Text);
        Assert.Contains("MetaThreads", allResult.Content[0].Text);
        Assert.DoesNotContain("secret-super-sensitive-token", allResult.Content[0].Text);
        Assert.DoesNotContain("credentialsEncrypted", allResult.Content[0].Text, StringComparison.OrdinalIgnoreCase);

        // 2. Filter by matching platform
        var threadsReq = new McpRequest("2.0", 61, "tools/call", JsonDocument.Parse("""
        {
            "name": "list_accounts",
            "arguments": { "platform": "Threads" }
        }
        """).RootElement);
        var threadsResp = await server.HandleRequestAsync(threadsReq, user.Id, CancellationToken.None);
        var threadsResult = Assert.IsType<McpToolCallResult>(threadsResp!.Result);
        Assert.False(threadsResult.IsError);
        Assert.Contains("vhogemann", threadsResult.Content[0].Text);

        // 3. Filter by non-matching platform
        var twitterReq = new McpRequest("2.0", 62, "tools/call", JsonDocument.Parse("""
        {
            "name": "list_accounts",
            "arguments": { "platform": "Twitter" }
        }
        """).RootElement);
        var twitterResp = await server.HandleRequestAsync(twitterReq, user.Id, CancellationToken.None);
        var twitterResult = Assert.IsType<McpToolCallResult>(twitterResp!.Result);
        Assert.False(twitterResult.IsError);
        Assert.Equal("[]", twitterResult.Content[0].Text?.Trim());
    }

    [Fact]
    public async Task HandleRequest_ToolsCall_AddFeed_PersistsSubscription()
    {
        var (server, user, db) = CreateTestServer();

        var addFeedParams = JsonDocument.Parse("""
        {
            "name": "add_feed",
            "arguments": {
                "title": "ICL Notícias",
                "feedUrl": "https://www.youtube.com/feeds/videos.xml?channel_id=UCTnRtQ9IkBGmCKcLB31BcyA",
                "websiteUrl": "https://www.youtube.com/@iclnoticias"
            }
        }
        """).RootElement;

        var request = new McpRequest("2.0", 70, "tools/call", addFeedParams);
        var response = await server.HandleRequestAsync(request, user.Id, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Null(response!.Error);
        var result = Assert.IsType<McpToolCallResult>(response.Result);
        Assert.False(result.IsError);

        var json = result.Content[0].Text!;
        Assert.Contains("ICL Notícias", json);
        Assert.Contains("UCTnRtQ9IkBGmCKcLB31BcyA", json);

        var feedDoc = JsonDocument.Parse(json);
        var feedId = Guid.Parse(feedDoc.RootElement.GetProperty("id").GetString()!);

        var persisted = await db.FeedSubscriptions.FindAsync(feedId);
        Assert.NotNull(persisted);
        Assert.Equal("ICL Notícias", persisted.Title);
        Assert.Equal(user.Id, persisted.UserId);
    }

    [Fact]
    public async Task HandleRequest_ToolsCall_UpdateFeed_UpdatesExistingSubscriptionFiltersAndTitle()
    {
        var (server, user, db) = CreateTestServer();

        var feed = FeedSubscription.Create(user.Id, new CreateFeedSubscriptionRequest(
            "Original Title",
            "https://example.com/rss.xml",
            null, null, null));
        db.FeedSubscriptions.Add(feed);
        await db.SaveChangesAsync();

        var updateFeedParams = JsonDocument.Parse($$"""
        {
            "name": "update_feed",
            "arguments": {
                "subscriptionId": "{{feed.Id}}",
                "title": "Updated Title",
                "includeFilters": "Bolsonaro, 6x1",
                "excludeFilters": "futebol, celebridade"
            }
        }
        """).RootElement;

        var request = new McpRequest("2.0", 71, "tools/call", updateFeedParams);
        var response = await server.HandleRequestAsync(request, user.Id, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Null(response!.Error);
        var result = Assert.IsType<McpToolCallResult>(response.Result);
        Assert.False(result.IsError);

        var json = result.Content[0].Text!;
        Assert.Contains("Updated Title", json);

        var persisted = await db.FeedSubscriptions.FindAsync(feed.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Updated Title", persisted.Title);
        Assert.Equal("Bolsonaro, 6x1", persisted.IncludeFilters);
        Assert.Equal("futebol, celebridade", persisted.ExcludeFilters);
    }

    [Fact]
    public async Task HandleRequest_ToolsCall_CreateWorkflow_CreatesWorkflowWithSourcesAndDestinations()
    {
        var (server, user, db) = CreateTestServer();

        var feed = FeedSubscription.Create(user.Id, new CreateFeedSubscriptionRequest(
            "Jones Manoel",
            "https://www.youtube.com/feeds/videos.xml?channel_id=UC02coXfDPjEmU8uDT2G8Z2A",
            null, null, null));
        db.FeedSubscriptions.Add(feed);

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Platform = "MetaThreads",
            Handle = "vhogemann",
            CredentialsEncrypted = "token",
            Status = "Active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var createWorkflowParams = JsonDocument.Parse($$"""
        {
            "name": "create_workflow",
            "arguments": {
                "title": "Esquerda Radical para Threads",
                "instructionPrompt": "Crie threads combativas para o Threads com video no primeiro post.",
                "feedSubscriptionIds": ["{{feed.Id}}"],
                "destinations": [
                    {
                        "platform": "MetaThreads",
                        "accountId": "{{account.Id}}",
                        "publishingMode": "Automatic",
                        "autoPublish": true
                    }
                ]
            }
        }
        """).RootElement;

        var request = new McpRequest("2.0", 80, "tools/call", createWorkflowParams);
        var response = await server.HandleRequestAsync(request, user.Id, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Null(response!.Error);
        var result = Assert.IsType<McpToolCallResult>(response.Result);
        Assert.False(result.IsError);

        var json = result.Content[0].Text!;
        Assert.Contains("Esquerda Radical para Threads", json);
        Assert.Contains("MetaThreads", json);
        Assert.Contains("vhogemann", json);

        var workflowDoc = JsonDocument.Parse(json);
        var workflowId = Guid.Parse(workflowDoc.RootElement.GetProperty("id").GetString()!);

        var persistedWf = await db.AutomationWorkflows.FindAsync(workflowId);
        Assert.NotNull(persistedWf);
        Assert.Equal("Esquerda Radical para Threads", persistedWf.Title);
    }

    [Fact]
    public async Task HandleRequest_ToolsCall_DeleteWorkflow_DeletesExistingWorkflow()
    {
        var (server, user, db) = CreateTestServer();

        var workflow = AutomationWorkflow.Create(user.Id, "Temporary Workflow", null, null);
        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        var deleteParams = JsonDocument.Parse($$"""
        {
            "name": "delete_workflow",
            "arguments": {
                "workflowId": "{{workflow.Id}}"
            }
        }
        """).RootElement;

        var request = new McpRequest("2.0", 72, "tools/call", deleteParams);
        var response = await server.HandleRequestAsync(request, user.Id, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Null(response!.Error);
        var result = Assert.IsType<McpToolCallResult>(response.Result);
        Assert.False(result.IsError);

        var persisted = await db.AutomationWorkflows.FindAsync(workflow.Id);
        Assert.Null(persisted);
    }

    [Fact]
    public async Task HandleRequest_ToolsCall_ConfigureAndGetReplyAgent_Succeeds()
    {
        var (server, user, db) = CreateTestServer();

        var workflow = AutomationWorkflow.Create(user.Id, "Reply Agent Test Workflow", null, null);
        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        // 1. configure_reply_agent
        var configParams = JsonDocument.Parse($$"""
        {
            "name": "configure_reply_agent",
            "arguments": {
                "workflowId": "{{workflow.Id}}",
                "isEnabled": true,
                "personaPrompt": "You are a witty, factual social worker bot.",
                "minRankingScore": 8,
                "maxRepliesPerUser": 2,
                "autoPublishReplies": false
            }
        }
        """).RootElement;

        var configReq = new McpRequest("2.0", 91, "tools/call", configParams);
        var configResp = await server.HandleRequestAsync(configReq, user.Id, CancellationToken.None);
        Assert.NotNull(configResp);
        Assert.Null(configResp!.Error);
        var configResult = Assert.IsType<McpToolCallResult>(configResp.Result);
        Assert.False(configResult.IsError);

        var agentDoc = JsonDocument.Parse(configResult.Content[0].Text!);
        Assert.Equal("You are a witty, factual social worker bot.", agentDoc.RootElement.GetProperty("personaPrompt").GetString());
        Assert.Equal(8, agentDoc.RootElement.GetProperty("minRankingScore").GetInt32());
        Assert.False(agentDoc.RootElement.GetProperty("autoPublishReplies").GetBoolean());

        // 2. get_reply_agent
        var getParams = JsonDocument.Parse($$"""
        {
            "name": "get_reply_agent",
            "arguments": {
                "workflowId": "{{workflow.Id}}"
            }
        }
        """).RootElement;

        var getReq = new McpRequest("2.0", 92, "tools/call", getParams);
        var getResp = await server.HandleRequestAsync(getReq, user.Id, CancellationToken.None);
        Assert.NotNull(getResp);
        Assert.Null(getResp!.Error);
        var getResult = Assert.IsType<McpToolCallResult>(getResp.Result);
        Assert.False(getResult.IsError);

        var getDoc = JsonDocument.Parse(getResult.Content[0].Text!);
        Assert.True(getDoc.RootElement.GetProperty("configured").GetBoolean());
        Assert.Equal(8, getDoc.RootElement.GetProperty("agent").GetProperty("minRankingScore").GetInt32());
        Assert.Equal(0, getDoc.RootElement.GetProperty("stats").GetProperty("totalAudited").GetInt32());
    }

    [Fact]
    public async Task HandleRequest_ToolsCall_TriggerReplyCheck_EnqueuesTask()
    {
        var taskQueue = new TestBackgroundTaskQueue();
        var (server, user, db) = CreateTestServer(customQueue: taskQueue);

        var workflow = AutomationWorkflow.Create(user.Id, "Polled Workflow", null, null);
        db.AutomationWorkflows.Add(workflow);

        var agent = ReplyAgent.Create(user.Id, "Persona Agent", "Persona", autoPublishReplies: true);
        db.ReplyAgents.Add(agent);
        workflow.ReplyAgentId = agent.Id;

        var draft = Draft.Create(user.Id, "Workflow Post Draft", "Content");
        draft.AutomationWorkflowId = workflow.Id;
        db.Drafts.Add(draft);

        var account = new Account { Id = Guid.NewGuid(), UserId = user.Id, Platform = "Bluesky", Handle = "test.bsky.social" };
        db.Accounts.Add(account);

        var pt = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Bluesky",
            AccountId = account.Id
        };
        db.PlatformThreads.Add(pt);

        var post = new Post
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            PlatformThreadId = pt.Id,
            Platform = "Bluesky",
            RemoteId = "at://did:plc:test/app.bsky.feed.post/post123",
            PostedAt = DateTime.UtcNow
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        var triggerParams = JsonDocument.Parse($$"""
        {
            "name": "trigger_reply_check",
            "arguments": {
                "workflowId": "{{workflow.Id}}",
                "runSynchronously": false
            }
        }
        """).RootElement;

        var req = new McpRequest("2.0", 93, "tools/call", triggerParams);
        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);

        Assert.NotNull(resp);
        Assert.Null(resp!.Error);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);
        Assert.Contains("Enqueued reply check for 1 post(s)", result.Content[0].Text);
        Assert.Contains(taskQueue.EnqueuedTasks, t => t.TaskType == "workflow-reply-check-post");
    }

    [Fact]
    public async Task HandleRequest_ToolsCall_ListCommentAudits_FiltersByDecision()
    {
        var (server, user, db) = CreateTestServer();

        var workflow = AutomationWorkflow.Create(user.Id, "Audit Workflow", null, null);
        db.AutomationWorkflows.Add(workflow);

        var agent = ReplyAgent.Create(user.Id, "Persona Agent", "Persona", autoPublishReplies: true);
        db.ReplyAgents.Add(agent);
        workflow.ReplyAgentId = agent.Id;

        var draft = Draft.Create(user.Id, "Audit Draft", "Content");
        draft.AutomationWorkflowId = workflow.Id;
        db.Drafts.Add(draft);

        var account = new Account { Id = Guid.NewGuid(), UserId = user.Id, Platform = "Bluesky", Handle = "test.bsky.social" };
        db.Accounts.Add(account);

        var pt = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Bluesky",
            AccountId = account.Id
        };
        db.PlatformThreads.Add(pt);

        var post = new Post
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            PlatformThreadId = pt.Id,
            Platform = "Bluesky",
            RemoteId = "at://did:plc:test/app.bsky.feed.post/post123",
            PostedAt = DateTime.UtcNow
        };
        db.Posts.Add(post);

        var audit1 = WorkflowCommentAudit.Create(
            agent.Id, post.Id, "Bluesky", "at://did:plc:other/post/c1", "user1.bsky.social", "did:plc:u1",
            "Great post!", DateTime.UtcNow, 9, "Praise", "Replied", generatedReplyText: "Thank you!");

        var audit2 = WorkflowCommentAudit.Create(
            agent.Id, post.Id, "Bluesky", "at://did:plc:other/post/c2", "user2.bsky.social", "did:plc:u2",
            "Spam comment", DateTime.UtcNow, 2, "Spam", "SkippedBelowThreshold");

        db.WorkflowCommentAudits.AddRange(audit1, audit2);
        await db.SaveChangesAsync();

        var listParams = JsonDocument.Parse($$"""
        {
            "name": "list_comment_audits",
            "arguments": {
                "workflowId": "{{workflow.Id}}",
                "decision": "Replied"
            }
        }
        """).RootElement;

        var req = new McpRequest("2.0", 94, "tools/call", listParams);
        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);

        Assert.NotNull(resp);
        Assert.Null(resp!.Error);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);

        var json = result.Content[0].Text!;
        Assert.Contains("Great post!", json);
        Assert.DoesNotContain("Spam comment", json);
    }

    [Fact]
    public async Task HandleRequest_ToolsCall_ReplyToComment_ApprovePendingAudit_Succeeds()
    {
        var (server, user, db) = CreateTestServer();

        var workflow = AutomationWorkflow.Create(user.Id, "Manual Reply Workflow", null, null);
        db.AutomationWorkflows.Add(workflow);

        var agent = ReplyAgent.Create(user.Id, "Persona Agent", "Persona", autoPublishReplies: false);
        db.ReplyAgents.Add(agent);
        workflow.ReplyAgentId = agent.Id;

        var draft = Draft.Create(user.Id, "Manual Reply Draft", "Content");
        draft.AutomationWorkflowId = workflow.Id;
        db.Drafts.Add(draft);

        var account = new Account { Id = Guid.NewGuid(), UserId = user.Id, Platform = "Bluesky", Handle = "test.bsky.social" };
        db.Accounts.Add(account);

        var pt = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Bluesky",
            AccountId = account.Id
        };
        db.PlatformThreads.Add(pt);

        var post = new Post
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            PlatformThreadId = pt.Id,
            Platform = "Bluesky",
            RemoteId = "at://did:plc:test/app.bsky.feed.post/post123",
            PostedAt = DateTime.UtcNow
        };
        db.Posts.Add(post);

        var audit = WorkflowCommentAudit.Create(
            agent.Id, post.Id, "Bluesky", "at://did:plc:other/post/c1", "user1.bsky.social", "did:plc:u1",
            "Can you clarify this point?", DateTime.UtcNow, 8, "Question", "PendingApproval",
            generatedReplyText: "Here is the clarification you requested.");
        db.WorkflowCommentAudits.Add(audit);
        await db.SaveChangesAsync();

        var replyParams = JsonDocument.Parse($$"""
        {
            "name": "reply_to_comment",
            "arguments": {
                "auditId": "{{audit.Id}}"
            }
        }
        """).RootElement;

        var req = new McpRequest("2.0", 95, "tools/call", replyParams);
        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);

        Assert.NotNull(resp);
        Assert.Null(resp!.Error);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);

        var updatedAudit = await db.WorkflowCommentAudits.FindAsync(audit.Id);
        Assert.NotNull(updatedAudit);
        Assert.Equal("Replied", updatedAudit.RankingDecision);
        Assert.Equal("at://did:plc:test/app.bsky.feed.post/reply123", updatedAudit.RemoteReplyId);
    }

    [Fact]
    public async Task HandleRequest_ResourcesList_ReturnsReplyAgentResource()
    {
        var (server, user, db) = CreateTestServer();

        var workflow = AutomationWorkflow.Create(user.Id, "Resource Workflow", null, null);
        db.AutomationWorkflows.Add(workflow);

        var agent = ReplyAgent.Create(user.Id, "Resource Persona", "Custom Persona", autoPublishReplies: true);
        db.ReplyAgents.Add(agent);
        workflow.ReplyAgentId = agent.Id;
        await db.SaveChangesAsync();

        // 1. resources/list
        var listReq = new McpRequest("2.0", 96, "resources/list", null);
        var listResp = await server.HandleRequestAsync(listReq, user.Id, CancellationToken.None);
        Assert.NotNull(listResp);
        Assert.Null(listResp!.Error);

        var listJson = JsonSerializer.Serialize(listResp.Result);
        var expectedUri = $"socialworker://workflows/{workflow.Id}/reply-agent";
        Assert.Contains(expectedUri, listJson);

        // 2. resources/read
        var readParams = JsonDocument.Parse($$"""
        {
            "uri": "{{expectedUri}}"
        }
        """).RootElement;
        var readReq = new McpRequest("2.0", 97, "resources/read", readParams);
        var readResp = await server.HandleRequestAsync(readReq, user.Id, CancellationToken.None);
        Assert.NotNull(readResp);
        Assert.Null(readResp!.Error);

        var readJson = JsonSerializer.Serialize(readResp.Result);
        Assert.Contains("Custom Persona", readJson);
    }

    [Fact]
    public async Task ListPosts_ReturnsPostsWithCommentCountsAndFilters()
    {
        var (server, user, db) = CreateTestServer();

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Drafts.Add(draft);

        var thread1 = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "MetaThreads",
            Stage = PlatformThreadStage.Sent
        };
        var thread2 = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "Bluesky",
            Stage = PlatformThreadStage.Sent
        };
        db.PlatformThreads.AddRange(thread1, thread2);

        var postThreads = new Post
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            PlatformThreadId = thread1.Id,
            Platform = "MetaThreads",
            RemoteId = "threads_post_1",
            Url = "https://threads.net/@test/post/1",
            PostedAt = DateTime.UtcNow.AddHours(-2)
        };
        var postBluesky = new Post
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            PlatformThreadId = thread2.Id,
            Platform = "Bluesky",
            RemoteId = "bsky_post_1",
            Url = "https://bsky.app/profile/test/post/1",
            PostedAt = DateTime.UtcNow.AddHours(-1)
        };
        db.Posts.AddRange(postThreads, postBluesky);

        var agent = ReplyAgent.Create(user.Id, "Test Agent", personaPrompt: "Test");
        db.ReplyAgents.Add(agent);

        var audit = WorkflowCommentAudit.Create(
            agent.Id,
            postThreads.Id,
            "MetaThreads",
            "c1",
            "commenter",
            "user1",
            "Awesome post!",
            DateTime.UtcNow.AddMinutes(-30),
            8,
            "Compliment",
            "Reply",
            "Great sentiment");
        db.WorkflowCommentAudits.Add(audit);
        await db.SaveChangesAsync();

        var allParams = JsonDocument.Parse("{}").RootElement;
        var allReq = new McpRequest("2.0", 1, "tools/call", JsonDocument.Parse("""{"name": "list_posts", "arguments": {}}""").RootElement);
        var allResp = await server.HandleRequestAsync(allReq, user.Id, CancellationToken.None);
        Assert.NotNull(allResp);
        var allResult = Assert.IsType<McpToolCallResult>(allResp.Result);
        Assert.False(allResult.IsError);

        var allText = allResult.Content[0].Text!;
        Assert.Contains("threads_post_1", allText);
        Assert.Contains("bsky_post_1", allText);
        Assert.Contains("\"commentsCount\": 1", allText);

        var filterReq = new McpRequest("2.0", 2, "tools/call", JsonDocument.Parse("""{"name": "list_posts", "arguments": {"platform": "MetaThreads", "hasReplies": true}}""").RootElement);
        var filterResp = await server.HandleRequestAsync(filterReq, user.Id, CancellationToken.None);
        Assert.NotNull(filterResp);
        var filterResult = Assert.IsType<McpToolCallResult>(filterResp.Result);
        Assert.False(filterResult.IsError);

        var filterText = filterResult.Content[0].Text!;
        Assert.Contains("threads_post_1", filterText);
        Assert.DoesNotContain("bsky_post_1", filterText);
    }

    [Fact]
    public async Task HandleRequestAsync_AssignAndListAndUnassignReplyAgentPost_Success()
    {
        var (server, user, db) = CreateTestServer();

        var agent = ReplyAgent.Create(user.Id, "Organic Post Monitor", "You are friendly");
        db.ReplyAgents.Add(agent);
        await db.SaveChangesAsync();

        var assignArgs = JsonDocument.Parse($$"""
        {
            "name": "assign_reply_agent_post",
            "arguments": {
                "agentId": "{{agent.Id}}",
                "remoteId": "at://did:plc:test/app.bsky.feed.post/organic_123",
                "platform": "Bluesky"
            }
        }
        """).RootElement;

        var assignReq = new McpRequest("2.0", 1, "tools/call", assignArgs);
        var assignResp = await server.HandleRequestAsync(assignReq, user.Id, CancellationToken.None);
        Assert.NotNull(assignResp);
        var assignResult = Assert.IsType<McpToolCallResult>(assignResp.Result);
        Assert.False(assignResult.IsError);
        Assert.Contains("Organic Post Monitor", assignResult.Content[0].Text);
        Assert.Contains("organic_123", assignResult.Content[0].Text);

        var listArgs = JsonDocument.Parse($$"""
        {
            "name": "list_reply_agent_posts",
            "arguments": {
                "agentId": "{{agent.Id}}"
            }
        }
        """).RootElement;
        var listReq = new McpRequest("2.0", 2, "tools/call", listArgs);
        var listResp = await server.HandleRequestAsync(listReq, user.Id, CancellationToken.None);
        Assert.NotNull(listResp);
        var listResult = Assert.IsType<McpToolCallResult>(listResp.Result);
        Assert.False(listResult.IsError);
        Assert.Contains("organic_123", listResult.Content[0].Text);

        var listPostsArgs = JsonDocument.Parse("""
        {
            "name": "list_posts",
            "arguments": {}
        }
        """).RootElement;
        var listPostsReq = new McpRequest("2.0", 3, "tools/call", listPostsArgs);
        var listPostsResp = await server.HandleRequestAsync(listPostsReq, user.Id, CancellationToken.None);
        Assert.NotNull(listPostsResp);
        var listPostsResult = Assert.IsType<McpToolCallResult>(listPostsResp.Result);
        Assert.False(listPostsResult.IsError);
        Assert.Contains("organic_123", listPostsResult.Content[0].Text);

        var assignedPost = await db.Posts.FirstAsync(p => p.RemoteId == "at://did:plc:test/app.bsky.feed.post/organic_123");

        var unassignArgs = JsonDocument.Parse($$"""
        {
            "name": "unassign_reply_agent_post",
            "arguments": {
                "agentId": "{{agent.Id}}",
                "postId": "{{assignedPost.Id}}"
            }
        }
        """).RootElement;
        var unassignReq = new McpRequest("2.0", 4, "tools/call", unassignArgs);
        var unassignResp = await server.HandleRequestAsync(unassignReq, user.Id, CancellationToken.None);
        Assert.NotNull(unassignResp);
        var unassignResult = Assert.IsType<McpToolCallResult>(unassignResp.Result);
        Assert.False(unassignResult.IsError);
        Assert.Contains("\"success\": true", unassignResult.Content[0].Text);

        var activeListResp = await server.HandleRequestAsync(listReq, user.Id, CancellationToken.None);
        Assert.NotNull(activeListResp);
        var activeListResult = Assert.IsType<McpToolCallResult>(activeListResp.Result);
        Assert.False(activeListResult.IsError);
        Assert.Equal("[]", activeListResult.Content[0].Text.Trim());
    }

    [Fact]
    public async Task HandleRequest_CallTool_CreateDraft_WithAccountId_AssociatesAccountWithThread()
    {
        var (server, user, db) = CreateTestServer();
        var accountId = Guid.NewGuid();
        db.Accounts.Add(new Account
        {
            Id = accountId,
            UserId = user.Id,
            Platform = "Twitter",
            Handle = "techuser",
            CredentialsEncrypted = "dummy"
        });
        await db.SaveChangesAsync();

        var callArgs = JsonDocument.Parse($$"""
        {
            "name": "create_draft",
            "arguments": {
                "title": "Account Draft",
                "content": "Tweet content here",
                "platform": "Twitter",
                "accountId": "{{accountId}}"
            }
        }
        """).RootElement;

        var req = new McpRequest("2.0", 5, "tools/call", callArgs);
        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);

        Assert.NotNull(resp);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);
        Assert.Contains("Draft created successfully with ID:", result.Content[0].Text);

        var draft = await db.Drafts.Include(d => d.Threads).FirstAsync(d => d.Title == "Account Draft");
        Assert.Equal(accountId, draft.TargetAccountId);
        var thread = Assert.Single(draft.Threads);
        Assert.Equal(accountId, thread.AccountId);
        Assert.Equal("Twitter", thread.Platform);
    }

    [Fact]
    public async Task HandleRequest_CallTool_ValidateDraft_WithoutExplicitPlatform_UsesDraftPlatform()
    {
        var (server, user, db) = CreateTestServer();
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "LinkedIn Draft",
            Content = "Professional update for LinkedIn network."
        };
        draft.Threads.Add(new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "LinkedIn",
            Content = draft.Content,
            Stage = PlatformThreadStage.Draft
        });
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var callArgs = JsonDocument.Parse($$"""
        {
            "name": "validate_draft",
            "arguments": {
                "draftId": "{{draft.Id}}"
            }
        }
        """).RootElement;

        var req = new McpRequest("2.0", 6, "tools/call", callArgs);
        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);

        Assert.NotNull(resp);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);
        Assert.Contains("Platform: LinkedIn", result.Content[0].Text);
    }

    [Fact]
    public async Task HandleRequest_CallTool_ListPosts_ReturnsAuditedCommentsCountAndPublishedText()
    {
        var (server, user, db) = CreateTestServer();
        var draft = new Draft { Id = Guid.NewGuid(), UserId = user.Id, Title = "D", Content = "C" };
        var thread = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft.Id, Platform = "Bluesky", Content = "C", Stage = PlatformThreadStage.Sent };
        var post = new Post
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DraftId = draft.Id,
            PlatformThreadId = thread.Id,
            Platform = "Bluesky",
            RemoteId = "at://did:plc:test/app.bsky.feed.post/audit_test",
            PublishedText = "Hello from live publish!",
            AuthorHandle = "publisher.bsky.social",
            PostedAt = DateTime.UtcNow
        };
        db.Drafts.Add(draft);
        db.PlatformThreads.Add(thread);
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        var callArgs = JsonDocument.Parse("""
        {
            "name": "list_posts",
            "arguments": {}
        }
        """).RootElement;

        var req = new McpRequest("2.0", 7, "tools/call", callArgs);
        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);

        Assert.NotNull(resp);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);
        var text = result.Content[0].Text;
        Assert.Contains("Hello from live publish!", text);
        Assert.Contains("\"auditedCommentsCount\": 0", text);
        Assert.Contains("publisher.bsky.social", text);
    }

    [Fact]
    public async Task HandleRequest_ToolsList_ReturnsAll68Tools()
    {
        var (server, user, _) = CreateTestServer();
        var request = new McpRequest("2.0", 2, "tools/list", null);

        var response = await server.HandleRequestAsync(request, user.Id, CancellationToken.None);

        Assert.NotNull(response);
        var json = JsonSerializer.Serialize(response.Result);
        using var doc = JsonDocument.Parse(json);
        var tools = doc.RootElement.GetProperty("tools").EnumerateArray().ToList();
        Assert.Equal(69, tools.Count);

        var toolNames = tools.Select(t => t.GetProperty("name").GetString()!).ToHashSet();
        Assert.Contains("import_media_from_url", toolNames);
        Assert.Contains("upload_media", toolNames);
        Assert.Contains("view_image", toolNames);
        Assert.Contains("list_templates", toolNames);
        Assert.Contains("render_template", toolNames);
        Assert.Contains("render_code_blocks", toolNames);
        Assert.Contains("search_images", toolNames);
        Assert.Contains("update_media", toolNames);
        Assert.Contains("delete_media", toolNames);
        Assert.Contains("schedule_post", toolNames);
        Assert.Contains("remove_queue_item", toolNames);
        Assert.Contains("reorder_queue", toolNames);
        Assert.Contains("pause_queue_item", toolNames);
        Assert.Contains("retry_queue_item", toolNames);
        Assert.Contains("update_schedule", toolNames);
        Assert.Contains("delete_schedule", toolNames);
        Assert.Contains("delete_feed", toolNames);
        Assert.Contains("delete_reply_agent", toolNames);
        Assert.Contains("get_workflow", toolNames);
        Assert.Contains("generate_variants", toolNames);
        Assert.Contains("set_bluesky_reply_target", toolNames);
        Assert.Contains("transcribe_video", toolNames);
        Assert.Contains("web_search", toolNames);
        Assert.Contains("export_workflow", toolNames);
        Assert.Contains("import_workflow", toolNames);
    }

    [Fact]
    public async Task McpTools_DoNotDriftFromDocumentation()
    {
        var (server, user, _) = CreateTestServer();
        var response = await server.HandleRequestAsync(new McpRequest("2.0", 1, "tools/list", null), user.Id, CancellationToken.None);
        Assert.NotNull(response);
        var json = JsonSerializer.Serialize(response!.Result);
        using var doc = JsonDocument.Parse(json);
        var toolNames = doc.RootElement.GetProperty("tools")
            .EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()!)
            .OrderBy(n => n)
            .ToList();

        var candidates = new[] { "/docs/mcp_server.md", Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../docs/mcp_server.md")) };
        var foundPath = candidates.FirstOrDefault(File.Exists);
        if (foundPath != null)
        {
            var docsContent = await File.ReadAllTextAsync(foundPath);
            foreach (var name in toolNames)
            {
                Assert.Contains($"`{name}`", docsContent);
            }
        }
    }

    [Fact]
    public async Task HandleRequest_ToolsCall_WorkflowPortability_ExportAndImport_Succeeds()
    {
        var (server, user, db) = CreateTestServer();
        var feed = FeedSubscription.Create(user.Id, "MCP Feed", "https://example.com/mcp.xml");
        db.FeedSubscriptions.Add(feed);

        var wf = AutomationWorkflow.Create(user.Id, "MCP Export WF", "Instruction prompt", null, 8);
        wf.WorkflowSources.Add(AutomationWorkflowSource.CreateFeedSource(wf.Id, feed.Id));
        wf.Destinations.Add(AutomationWorkflowDestination.Create(wf.Id, "Bluesky", WorkflowPublishingMode.Manual));
        db.AutomationWorkflows.Add(wf);
        await db.SaveChangesAsync();

        var exportArgs = JsonDocument.Parse($$"""
        {
            "name": "export_workflow",
            "arguments": {
                "workflowId": "{{wf.Id}}"
            }
        }
        """).RootElement;

        var exportReq = new McpRequest("2.0", 80, "tools/call", exportArgs);
        var exportResp = await server.HandleRequestAsync(exportReq, user.Id, CancellationToken.None);

        Assert.NotNull(exportResp);
        var exportResult = Assert.IsType<McpToolCallResult>(exportResp.Result);
        Assert.False(exportResult.IsError);
        var exportJson = exportResult.Content[0].Text!;
        Assert.Contains("MCP Export WF", exportJson);
        Assert.Contains("https://example.com/mcp.xml", exportJson);

        var user2 = new AppUser { Id = Guid.NewGuid(), Username = "user2", PasswordHash = "hash", Role = "User", CreatedAt = DateTime.UtcNow };
        db.Users.Add(user2);
        await db.SaveChangesAsync();

        var importArgs = JsonDocument.Parse($$"""
        {
            "name": "import_workflow",
            "arguments": {
                "workflowJson": {{JsonSerializer.Serialize(exportJson)}}
            }
        }
        """).RootElement;

        var importReq = new McpRequest("2.0", 81, "tools/call", importArgs);
        var importResp = await server.HandleRequestAsync(importReq, user2.Id, CancellationToken.None);

        Assert.NotNull(importResp);
        var importResult = Assert.IsType<McpToolCallResult>(importResp.Result);
        Assert.False(importResult.IsError);
        Assert.Contains("Successfully imported 1 workflow(s)", importResult.Content[0].Text!);
    }

    [Fact]
    public async Task UploadMedia_ValidBase64_AttachesToDraft()
    {
        var (server, user, db) = CreateTestServer();
        var draft = Draft.Create(user.Id, "Draft for media", "Testing upload media", SocialPlatform.Bluesky);
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var base64Data = Convert.ToBase64String(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82 });
        var uploadArgs = JsonDocument.Parse($$"""
        {
            "name": "upload_media",
            "arguments": {
                "draftId": "{{draft.Id}}",
                "fileName": "chart.png",
                "base64Data": "{{base64Data}}",
                "mimeType": "image/png",
                "altText": "A test chart image"
            }
        }
        """).RootElement;

        var req = new McpRequest("2.0", 50, "tools/call", uploadArgs);
        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);

        Assert.NotNull(resp);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);
        var text = result.Content[0].Text!;
        Assert.Contains("media://", text);
        Assert.Contains("mediaId", text);

        var asset = await db.MediaAssets.FirstOrDefaultAsync(a => a.DraftId == draft.Id);
        Assert.NotNull(asset);
        Assert.Equal("A test chart image", asset.AltText);
    }

    [Fact]
    public async Task ListTemplates_And_RenderTemplate_Succeeds()
    {
        var (server, user, db) = CreateTestServer();
        var draft = Draft.Create(user.Id, "Template test", "Testing templates", SocialPlatform.Bluesky);
        var template = Template.Create(
            user.Id,
            "Highlight Card",
            "Card for highlights",
            1200,
            630,
            """{"name": "Highlight Card", "slots": [{"name": "headline", "type": "text"}]}""");
        db.Drafts.Add(draft);
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var listArgs = JsonDocument.Parse("""
        {
            "name": "list_templates",
            "arguments": {}
        }
        """).RootElement;
        var listReq = new McpRequest("2.0", 51, "tools/call", listArgs);
        var listResp = await server.HandleRequestAsync(listReq, user.Id, CancellationToken.None);
        Assert.NotNull(listResp);
        var listResult = Assert.IsType<McpToolCallResult>(listResp.Result);
        Assert.Contains("Highlight Card", listResult.Content[0].Text);

        var renderArgs = JsonDocument.Parse($$"""
        {
            "name": "render_template",
            "arguments": {
                "draftId": "{{draft.Id}}",
                "templateId": "{{template.Id}}",
                "slots": {
                    "headline": "Breaking Announcement"
                },
                "altText": "Announcement graphic"
            }
        }
        """).RootElement;
        var renderReq = new McpRequest("2.0", 52, "tools/call", renderArgs);
        var renderResp = await server.HandleRequestAsync(renderReq, user.Id, CancellationToken.None);
        Assert.NotNull(renderResp);
        var renderResult = Assert.IsType<McpToolCallResult>(renderResp.Result);
        Assert.False(renderResult.IsError);
        Assert.Contains("media://", renderResult.Content[0].Text);
    }

    [Fact]
    public async Task UpdateMedia_And_DeleteMedia_Succeeds()
    {
        var (server, user, db) = CreateTestServer();
        var draft = Draft.Create(user.Id, "Media crud", "Testing crud", SocialPlatform.Bluesky);
        var asset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            FileName = "test.png",
            MimeType = "image/png",
            FilePath = "test.png",
            Sha256 = "dummy",
            SizeBytes = 100,
            Width = 10,
            Height = 10,
            AltText = "Original text",
            CreatedAt = DateTime.UtcNow
        };
        db.Drafts.Add(draft);
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();

        var updateArgs = JsonDocument.Parse($$"""
        {
            "name": "update_media",
            "arguments": {
                "mediaId": "{{asset.Id}}",
                "altText": "Updated text"
            }
        }
        """).RootElement;
        var updateReq = new McpRequest("2.0", 53, "tools/call", updateArgs);
        var updateResp = await server.HandleRequestAsync(updateReq, user.Id, CancellationToken.None);
        Assert.NotNull(updateResp);
        var updateResult = Assert.IsType<McpToolCallResult>(updateResp.Result);
        Assert.False(updateResult.IsError);
        Assert.Contains("Updated text", updateResult.Content[0].Text);

        var deleteArgs = JsonDocument.Parse($$"""
        {
            "name": "delete_media",
            "arguments": {
                "mediaId": "{{asset.Id}}"
            }
        }
        """).RootElement;
        var deleteReq = new McpRequest("2.0", 54, "tools/call", deleteArgs);
        var deleteResp = await server.HandleRequestAsync(deleteReq, user.Id, CancellationToken.None);
        Assert.NotNull(deleteResp);
        var deleteResult = Assert.IsType<McpToolCallResult>(deleteResp.Result);
        Assert.False(deleteResult.IsError);

        var remaining = await db.MediaAssets.FirstOrDefaultAsync(a => a.Id == asset.Id);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task SchedulePost_EnqueuesWithTargetTimestamp()
    {
        var (server, user, db) = CreateTestServer();
        var schedule = Schedule.Create(user.Id, "Weekly News", "UTC");
        var draft = Draft.Create(user.Id, "Scheduled draft", "Content for scheduled post", SocialPlatform.Bluesky);
        var thread = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft.Id, Platform = "Bluesky", Content = "Target post text" };
        draft.Threads.Add(thread);
        db.Schedules.Add(schedule);
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var targetTime = DateTime.UtcNow.AddDays(2).ToString("o");
        var scheduleArgs = JsonDocument.Parse($$"""
        {
            "name": "schedule_post",
            "arguments": {
                "scheduleId": "{{schedule.Id}}",
                "draftId": "{{draft.Id}}",
                "publishAt": "{{targetTime}}"
            }
        }
        """).RootElement;

        var req = new McpRequest("2.0", 55, "tools/call", scheduleArgs);
        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);
        Assert.NotNull(resp);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);
        Assert.Contains("scheduled", result.Content[0].Text);

        var item = await db.ScheduleQueueItems.FirstOrDefaultAsync(q => q.DraftId == draft.Id);
        Assert.NotNull(item);
    }

    [Fact]
    public async Task QueueOperations_Remove_Reorder_Pause_Retry_Succeeds()
    {
        var (server, user, db) = CreateTestServer();
        var schedule = Schedule.Create(user.Id, "Ops Schedule", "UTC");
        var draft1 = Draft.Create(user.Id, "Draft 1", "Content 1", SocialPlatform.Bluesky);
        var thread1 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft1.Id, Platform = "Bluesky", Content = "Text 1" };
        draft1.Threads.Add(thread1);

        var draft2 = Draft.Create(user.Id, "Draft 2", "Content 2", SocialPlatform.Bluesky);
        var thread2 = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft2.Id, Platform = "Bluesky", Content = "Text 2" };
        draft2.Threads.Add(thread2);

        var queueItem1 = ScheduleQueueItem.Create(schedule.Id, draft1.Id, thread1.Id, 1);
        var queueItem2 = ScheduleQueueItem.Create(schedule.Id, draft2.Id, thread2.Id, 2);

        db.Schedules.Add(schedule);
        db.Drafts.AddRange(draft1, draft2);
        db.ScheduleQueueItems.AddRange(queueItem1, queueItem2);
        await db.SaveChangesAsync();

        var pauseArgs = JsonDocument.Parse($$"""
        {
            "name": "pause_queue_item",
            "arguments": {
                "scheduleId": "{{schedule.Id}}",
                "queueItemId": "{{queueItem1.Id}}"
            }
        }
        """).RootElement;
        var pauseResp = await server.HandleRequestAsync(new McpRequest("2.0", 56, "tools/call", pauseArgs), user.Id, CancellationToken.None);
        Assert.NotNull(pauseResp);
        var pauseResult = Assert.IsType<McpToolCallResult>(pauseResp.Result);
        Assert.False(pauseResult.IsError);

        var reorderArgs = JsonDocument.Parse($$"""
        {
            "name": "reorder_queue",
            "arguments": {
                "scheduleId": "{{schedule.Id}}",
                "itemIds": ["{{queueItem2.Id}}", "{{queueItem1.Id}}"]
            }
        }
        """).RootElement;
        var reorderResp = await server.HandleRequestAsync(new McpRequest("2.0", 57, "tools/call", reorderArgs), user.Id, CancellationToken.None);
        Assert.NotNull(reorderResp);
        var reorderResult = Assert.IsType<McpToolCallResult>(reorderResp.Result);
        Assert.False(reorderResult.IsError);

        var retryArgs = JsonDocument.Parse($$"""
        {
            "name": "retry_queue_item",
            "arguments": {
                "queueItemId": "{{queueItem1.Id}}"
            }
        }
        """).RootElement;
        var retryResp = await server.HandleRequestAsync(new McpRequest("2.0", 58, "tools/call", retryArgs), user.Id, CancellationToken.None);
        Assert.NotNull(retryResp);
        var retryResult = Assert.IsType<McpToolCallResult>(retryResp.Result);
        Assert.False(retryResult.IsError);

        var removeArgs = JsonDocument.Parse($$"""
        {
            "name": "remove_queue_item",
            "arguments": {
                "scheduleId": "{{schedule.Id}}",
                "queueItemId": "{{queueItem2.Id}}"
            }
        }
        """).RootElement;
        var removeResp = await server.HandleRequestAsync(new McpRequest("2.0", 59, "tools/call", removeArgs), user.Id, CancellationToken.None);
        Assert.NotNull(removeResp);
        var removeResult = Assert.IsType<McpToolCallResult>(removeResp.Result);
        Assert.False(removeResult.IsError);
    }

    [Fact]
    public async Task Workflow_GetWorkflow_And_UpdateStatus_Succeeds()
    {
        var (server, user, db) = CreateTestServer();
        var schedule = Schedule.Create(user.Id, "Flow Schedule", "UTC");
        db.Schedules.Add(schedule);
        var workflow = AutomationWorkflow.Create(
            user.Id,
            "Daily Digest",
            "Tone prompt",
            schedule.Id,
            7);
        db.AutomationWorkflows.Add(workflow);
        await db.SaveChangesAsync();

        var getArgs = JsonDocument.Parse($$"""
        {
            "name": "get_workflow",
            "arguments": {
                "workflowId": "{{workflow.Id}}"
            }
        }
        """).RootElement;
        var getResp = await server.HandleRequestAsync(new McpRequest("2.0", 60, "tools/call", getArgs), user.Id, CancellationToken.None);
        Assert.NotNull(getResp);
        var getResult = Assert.IsType<McpToolCallResult>(getResp.Result);
        Assert.False(getResult.IsError);
        Assert.Contains("Daily Digest", getResult.Content[0].Text);

        var updateArgs = JsonDocument.Parse($$"""
        {
            "name": "update_workflow",
            "arguments": {
                "workflowId": "{{workflow.Id}}",
                "title": "Updated Daily Digest"
            }
        }
        """).RootElement;
        var updateResp = await server.HandleRequestAsync(new McpRequest("2.0", 61, "tools/call", updateArgs), user.Id, CancellationToken.None);
        Assert.NotNull(updateResp);
        var updateResult = Assert.IsType<McpToolCallResult>(updateResp.Result);
        Assert.False(updateResult.IsError);
        Assert.Contains("Updated Daily Digest", updateResult.Content[0].Text);

        var refreshed = await db.AutomationWorkflows.FirstOrDefaultAsync(w => w.Id == workflow.Id);
        Assert.Equal("Updated Daily Digest", refreshed!.Title);
    }

    [Fact]
    public async Task Lifecycle_DeleteSchedule_Feed_ReplyAgent_Succeeds()
    {
        var (server, user, db) = CreateTestServer();
        var schedule = Schedule.Create(user.Id, "To Delete", "UTC");
        var feed = FeedSubscription.Create(user.Id, "RSS Feed", "https://example.com/feed.xml");
        var agent = ReplyAgent.Create(user.Id, "Temp Agent", "Helpful assistant");
        db.Schedules.Add(schedule);
        db.FeedSubscriptions.Add(feed);
        db.ReplyAgents.Add(agent);
        await db.SaveChangesAsync();

        var delSchedArgs = JsonDocument.Parse($$"""
        {
            "name": "delete_schedule",
            "arguments": { "scheduleId": "{{schedule.Id}}" }
        }
        """).RootElement;
        var delSchedResp = await server.HandleRequestAsync(new McpRequest("2.0", 62, "tools/call", delSchedArgs), user.Id, CancellationToken.None);
        Assert.NotNull(delSchedResp);
        Assert.False(Assert.IsType<McpToolCallResult>(delSchedResp.Result).IsError);

        var delFeedArgs = JsonDocument.Parse($$"""
        {
            "name": "delete_feed",
            "arguments": { "feedId": "{{feed.Id}}" }
        }
        """).RootElement;
        var delFeedResp = await server.HandleRequestAsync(new McpRequest("2.0", 63, "tools/call", delFeedArgs), user.Id, CancellationToken.None);
        Assert.NotNull(delFeedResp);
        Assert.False(Assert.IsType<McpToolCallResult>(delFeedResp.Result).IsError);

        var delAgentArgs = JsonDocument.Parse($$"""
        {
            "name": "delete_reply_agent",
            "arguments": { "agentId": "{{agent.Id}}" }
        }
        """).RootElement;
        var delAgentResp = await server.HandleRequestAsync(new McpRequest("2.0", 64, "tools/call", delAgentArgs), user.Id, CancellationToken.None);
        Assert.NotNull(delAgentResp);
        Assert.False(Assert.IsType<McpToolCallResult>(delAgentResp.Result).IsError);

        Assert.Null(await db.Schedules.FirstOrDefaultAsync(s => s.Id == schedule.Id));
        Assert.Null(await db.FeedSubscriptions.FirstOrDefaultAsync(f => f.Id == feed.Id));
        Assert.Null(await db.ReplyAgents.FirstOrDefaultAsync(a => a.Id == agent.Id));
    }

    [Fact]
    public async Task PublishThread_DryRun_ReturnsValidationWithoutPublishing()
    {
        var (server, user, db) = CreateTestServer();
        var account = new Account { Id = Guid.NewGuid(), UserId = user.Id, Platform = "Bluesky", Handle = "dryrun.bsky.social" };
        var draft = Draft.Create(user.Id, "Dry Run Draft", "Pre-validated text", SocialPlatform.Bluesky);
        var thread = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft.Id, Platform = "Bluesky", Content = "Pre-validated text" };
        draft.Threads.Add(thread);
        db.Accounts.Add(account);
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var dryRunArgs = JsonDocument.Parse($$"""
        {
            "name": "publish_thread",
            "arguments": {
                "draftId": "{{draft.Id}}",
                "platform": "Bluesky",
                "dryRun": true
            }
        }
        """).RootElement;

        var req = new McpRequest("2.0", 65, "tools/call", dryRunArgs);
        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);

        Assert.NotNull(resp);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);
        Assert.Contains("Dry-run preview and validation completed", result.Content[0].Text);

        var postCount = await db.Posts.CountAsync(p => p.DraftId == draft.Id);
        Assert.Equal(0, postCount);
    }

    [Fact]
    public async Task Tools_ViewImage_WithValidMediaAsset_ReturnsImageContentPartAndMetadata()
    {
        var (server, user, db, storage) = CreateTestServerWithStorage();
        var draft = Draft.Create(user.Id, "Draft with image", "Content", SocialPlatform.Bluesky);
        db.Drafts.Add(draft);

        var mediaId = Guid.NewGuid();
        var relativePath = Path.Combine("media", $"{mediaId}.png");
        var fullPath = storage.GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var dummyBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4 };
        await File.WriteAllBytesAsync(fullPath, dummyBytes);

        var asset = new MediaAsset
        {
            Id = mediaId,
            DraftId = draft.Id,
            FileName = "infographic.png",
            MimeType = "image/png",
            FilePath = relativePath,
            Sha256 = "dummy",
            SizeBytes = dummyBytes.Length,
            Width = 800,
            Height = 600,
            AltText = "Visual breakdown of Q3 revenue",
            CreatedAt = DateTime.UtcNow
        };
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();

        var callArgs = JsonDocument.Parse($$"""
        {
            "name": "view_image",
            "arguments": {
                "id": "{{asset.Id}}"
            }
        }
        """).RootElement;

        var req = new McpRequest("2.0", 66, "tools/call", callArgs);
        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);

        Assert.NotNull(resp);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);
        Assert.Equal(2, result.Content.Count);

        var textPart = result.Content.First(c => c.Type == "text");
        Assert.Contains("infographic.png", textPart.Text);
        Assert.Contains("Visual breakdown of Q3 revenue", textPart.Text);
        Assert.Contains($"media://{asset.Id}", textPart.Text);

        var imagePart = result.Content.First(c => c.Type == "image");
        Assert.Equal("image/png", imagePart.MimeType);
        Assert.False(string.IsNullOrWhiteSpace(imagePart.Data));

        var uriArgs = JsonDocument.Parse($$"""
        {
            "name": "view_image",
            "arguments": {
                "id": "media://{{asset.Id}}"
            }
        }
        """).RootElement;
        var uriResp = await server.HandleRequestAsync(new McpRequest("2.0", 67, "tools/call", uriArgs), user.Id, CancellationToken.None);
        Assert.NotNull(uriResp);
        var uriResult = Assert.IsType<McpToolCallResult>(uriResp.Result);
        Assert.False(uriResult.IsError);
        Assert.Contains("infographic.png", uriResult.Content.First(c => c.Type == "text").Text);
    }

    [Fact]
    public async Task Tools_ViewImage_WithNonExistentOrUnauthorizedAsset_ReturnsError()
    {
        var (server, user, db, _) = CreateTestServerWithStorage();

        var nonExistentArgs = JsonDocument.Parse($$"""
        {
            "name": "view_image",
            "arguments": {
                "id": "{{Guid.NewGuid()}}"
            }
        }
        """).RootElement;
        var resp1 = await server.HandleRequestAsync(new McpRequest("2.0", 68, "tools/call", nonExistentArgs), user.Id, CancellationToken.None);
        Assert.NotNull(resp1);
        var res1 = Assert.IsType<McpToolCallResult>(resp1.Result);
        Assert.True(res1.IsError);

        var otherUser = CreateSeedUser(db, "other", "other@example.com");
        var otherDraft = Draft.Create(otherUser.Id, "Other Draft", "Private", SocialPlatform.Bluesky);
        db.Drafts.Add(otherDraft);
        var otherAsset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            DraftId = otherDraft.Id,
            FileName = "secret.png",
            MimeType = "image/png",
            FilePath = "secret.png",
            Sha256 = "dummy",
            SizeBytes = 10,
            Width = 10,
            Height = 10
        };
        db.MediaAssets.Add(otherAsset);
        await db.SaveChangesAsync();

        var unauthorizedArgs = JsonDocument.Parse($$"""
        {
            "name": "view_image",
            "arguments": {
                "id": "{{otherAsset.Id}}"
            }
        }
        """).RootElement;
        var resp2 = await server.HandleRequestAsync(new McpRequest("2.0", 69, "tools/call", unauthorizedArgs), user.Id, CancellationToken.None);
        Assert.NotNull(resp2);
        var res2 = Assert.IsType<McpToolCallResult>(resp2.Result);
        Assert.True(res2.IsError);
    }

    [Fact]
    public async Task Tools_ListSources_IncludesMediaAssetsAsImageSources()
    {
        var (server, user, db, _) = CreateTestServerWithStorage();
        var draft = Draft.Create(user.Id, "Sourced draft", "Content", SocialPlatform.Bluesky);
        db.Drafts.Add(draft);

        var urlSource = Source.CreateUrl("https://example.com/article", "Example Article");
        urlSource.ProcessingStatus = SourceProcessingStatus.Complete;
        db.Sources.Add(urlSource);
        db.DraftSources.Add(new DraftSource { DraftId = draft.Id, SourceId = urlSource.Id });

        var mediaAsset = new MediaAsset
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            FileName = "revenue-chart.jpg",
            MimeType = "image/jpeg",
            FilePath = "chart.jpg",
            Sha256 = "dummy",
            SizeBytes = 5000,
            Width = 1200,
            Height = 800,
            AltText = "Revenue growth trajectory",
            CreatedAt = DateTime.UtcNow
        };
        db.MediaAssets.Add(mediaAsset);
        await db.SaveChangesAsync();

        var listArgs = JsonDocument.Parse($$"""
        {
            "name": "list_sources",
            "arguments": {
                "draftId": "{{draft.Id}}"
            }
        }
        """).RootElement;

        var req = new McpRequest("2.0", 70, "tools/call", listArgs);
        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);

        Assert.NotNull(resp);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);

        var text = result.Content[0].Text!;
        Assert.Contains("Example Article", text);
        Assert.Contains("revenue-chart.jpg", text);
        Assert.Contains("Image", text);
        Assert.Contains($"media://{mediaAsset.Id}", text);
        Assert.Contains("Revenue growth trajectory", text);
    }

    [Fact]
    public async Task Tools_FetchSource_WithMediaAssetId_ReturnsImageContent()
    {
        var (server, user, db, storage) = CreateTestServerWithStorage();
        var draft = Draft.Create(user.Id, "Draft for fetch", "Content", SocialPlatform.Bluesky);
        db.Drafts.Add(draft);

        var mediaId = Guid.NewGuid();
        var relativePath = Path.Combine("media", $"{mediaId}.png");
        var fullPath = storage.GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var dummyBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 5, 6, 7, 8 };
        await File.WriteAllBytesAsync(fullPath, dummyBytes);

        var asset = new MediaAsset
        {
            Id = mediaId,
            DraftId = draft.Id,
            FileName = "source-photo.png",
            MimeType = "image/png",
            FilePath = relativePath,
            Sha256 = "dummy",
            SizeBytes = dummyBytes.Length,
            Width = 400,
            Height = 300,
            AltText = "Conference photo",
            CreatedAt = DateTime.UtcNow
        };
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();

        var fetchArgs = JsonDocument.Parse($$"""
        {
            "name": "fetch_source",
            "arguments": {
                "sourceId": "{{asset.Id}}"
            }
        }
        """).RootElement;

        var req = new McpRequest("2.0", 71, "tools/call", fetchArgs);
        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);

        Assert.NotNull(resp);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);

        var textPart = result.Content.FirstOrDefault(c => c.Type == "text");
        Assert.NotNull(textPart);
        Assert.Contains("source-photo.png", textPart.Text);

        var imagePart = result.Content.FirstOrDefault(c => c.Type == "image");
        Assert.NotNull(imagePart);
        Assert.Equal("image/png", imagePart.MimeType);
        Assert.False(string.IsNullOrWhiteSpace(imagePart.Data));
    }

    [Fact]
    public async Task Resources_ReadResource_WithMediaUri_ReturnsBlobContent()
    {
        var (server, user, db, storage) = CreateTestServerWithStorage();
        var draft = Draft.Create(user.Id, "Resource draft", "Content", SocialPlatform.Bluesky);
        db.Drafts.Add(draft);

        var mediaId = Guid.NewGuid();
        var relativePath = Path.Combine("media", $"{mediaId}.jpg");
        var fullPath = storage.GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var dummyBytes = new byte[] { 255, 216, 255, 224, 0, 16, 74, 70, 73, 70 };
        await File.WriteAllBytesAsync(fullPath, dummyBytes);

        var asset = new MediaAsset
        {
            Id = mediaId,
            DraftId = draft.Id,
            FileName = "hero.jpg",
            MimeType = "image/jpeg",
            FilePath = relativePath,
            Sha256 = "dummy",
            SizeBytes = dummyBytes.Length,
            Width = 1920,
            Height = 1080,
            AltText = "Hero landscape",
            CreatedAt = DateTime.UtcNow
        };
        db.MediaAssets.Add(asset);
        await db.SaveChangesAsync();

        var readReq = new McpRequest("2.0", 72, "resources/read", JsonDocument.Parse($$"""{"uri": "socialworker://media/{{asset.Id}}"}""").RootElement);
        var resp = await server.HandleRequestAsync(readReq, user.Id, CancellationToken.None);

        Assert.NotNull(resp);
        var readResult = Assert.IsType<McpResourceReadResult>(resp.Result);
        var content = Assert.Single(readResult.Contents);
        Assert.Equal($"socialworker://media/{asset.Id}", content.Uri);
        Assert.Equal("image/jpeg", content.MimeType);
        Assert.NotNull(content.Blob);
        Assert.Equal(Convert.ToBase64String(dummyBytes), content.Blob);
        Assert.Null(content.Text);
    }

    [Fact]
    public async Task Tools_ListPosts_WithPlatformAliasThreads_ReturnsMetaThreadsPosts()
    {
        var (server, user, db) = CreateTestServer();
        var draft = Draft.Create(user.Id, "Threads Draft", "Content", SocialPlatform.MetaThreads);
        db.Drafts.Add(draft);
        var post = new Post
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DraftId = draft.Id,
            Platform = "MetaThreads",
            RemoteId = "threads-post-123",
            Url = "https://www.threads.net/@tester/post/threads-post-123",
            PublishedText = "Hello Threads"
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        var req = new McpRequest("2.0", 80, "tools/call", JsonDocument.Parse("""
        {
            "name": "list_posts",
            "arguments": {
                "platform": "Threads"
            }
        }
        """).RootElement);

        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);
        Assert.NotNull(resp);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);
        var doc = JsonDocument.Parse(result.Content[0].Text);
        var posts = doc.RootElement.EnumerateArray().ToList();
        Assert.Single(posts);
        Assert.Equal("threads-post-123", posts[0].GetProperty("remoteId").GetString());
        Assert.Equal("https://www.threads.net/@tester/post/threads-post-123", posts[0].GetProperty("url").GetString());
    }

    [Fact]
    public async Task Tools_ListPosts_WithDraftId_FiltersByDraftId()
    {
        var (server, user, db) = CreateTestServer();
        var draft1 = Draft.Create(user.Id, "Draft 1", "Content 1", SocialPlatform.MetaThreads);
        var draft2 = Draft.Create(user.Id, "Draft 2", "Content 2", SocialPlatform.Bluesky);
        db.Drafts.AddRange(draft1, draft2);

        var post1 = new Post { Id = Guid.NewGuid(), UserId = user.Id, DraftId = draft1.Id, Platform = "MetaThreads", RemoteId = "p1", Url = "https://example.com/1" };
        var post2 = new Post { Id = Guid.NewGuid(), UserId = user.Id, DraftId = draft2.Id, Platform = "Bluesky", RemoteId = "p2", Url = "https://example.com/2" };
        db.Posts.AddRange(post1, post2);
        await db.SaveChangesAsync();

        var req = new McpRequest("2.0", 81, "tools/call", JsonDocument.Parse($$"""
        {
            "name": "list_posts",
            "arguments": {
                "draftId": "{{draft1.Id}}"
            }
        }
        """).RootElement);

        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);
        Assert.NotNull(resp);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);
        var doc = JsonDocument.Parse(result.Content[0].Text);
        var posts = doc.RootElement.EnumerateArray().ToList();
        Assert.Single(posts);
        Assert.Equal("p1", posts[0].GetProperty("remoteId").GetString());
    }

    [Fact]
    public async Task Tools_GetDraft_WhenThreadPublished_ReturnsIsPublishedTrueAndPublishedUrls()
    {
        var (server, user, db) = CreateTestServer();
        var draft = Draft.Create(user.Id, "Published Draft", "Content", SocialPlatform.MetaThreads);
        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = draft.Id,
            Platform = "MetaThreads",
            Stage = PlatformThreadStage.Sent,
            Content = "Content"
        };
        draft.Threads.Add(thread);
        var post = new Post
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            DraftId = draft.Id,
            PlatformThreadId = thread.Id,
            Platform = "MetaThreads",
            RemoteId = "post-remote-999",
            Url = "https://www.threads.net/@tester/post/post-remote-999",
            PublishedText = "Content"
        };
        db.Drafts.Add(draft);
        db.Posts.Add(post);
        await db.SaveChangesAsync();

        var req = new McpRequest("2.0", 82, "tools/call", JsonDocument.Parse($$"""
        {
            "name": "get_draft",
            "arguments": {
                "draftId": "{{draft.Id}}"
            }
        }
        """).RootElement);

        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);
        Assert.NotNull(resp);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);
        var doc = JsonDocument.Parse(result.Content[0].Text);
        Assert.True(doc.RootElement.GetProperty("isPublished").GetBoolean());
        var urls = doc.RootElement.GetProperty("publishedUrls").EnumerateArray().Select(u => u.GetString()).ToList();
        Assert.Contains("https://www.threads.net/@tester/post/post-remote-999", urls);
    }

    [Fact]
    public async Task Tools_PublishThread_ReturnsStructuredJsonWithPostsAndUrls()
    {
        var stubPublisher = new ConfigurableStubPublisher("MetaThreads", new List<PublishedPost>
        {
            new() { SegmentIndex = 0, RemoteId = "remote-1234", Url = "https://www.threads.net/@tester/post/1234" }
        });
        var publisherResolver = new ConfigurablePublisherResolver(stubPublisher);

        var (server, user, db) = CreateTestServer(customPublisherResolver: publisherResolver);
        var account = new Account { Id = Guid.NewGuid(), UserId = user.Id, Platform = "MetaThreads", Handle = "tester" };
        var draft = Draft.Create(user.Id, "Threads Publish Draft", "Hello Threads", SocialPlatform.MetaThreads);
        var thread = new PlatformThread { Id = Guid.NewGuid(), DraftId = draft.Id, Platform = "MetaThreads", Content = "Hello Threads" };
        draft.Threads.Add(thread);
        db.Accounts.Add(account);
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        var req = new McpRequest("2.0", 83, "tools/call", JsonDocument.Parse($$"""
        {
            "name": "publish_thread",
            "arguments": {
                "draftId": "{{draft.Id}}",
                "platform": "Threads",
                "confirm": true
            }
        }
        """).RootElement);

        var resp = await server.HandleRequestAsync(req, user.Id, CancellationToken.None);
        Assert.NotNull(resp);
        var result = Assert.IsType<McpToolCallResult>(resp.Result);
        Assert.False(result.IsError);

        var doc = JsonDocument.Parse(result.Content[0].Text);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        var urls = doc.RootElement.GetProperty("publishedUrls").EnumerateArray().Select(u => u.GetString()).ToList();
        Assert.Contains("https://www.threads.net/@tester/post/1234", urls);
        var posts = doc.RootElement.GetProperty("posts").EnumerateArray().ToList();
        Assert.Single(posts);
        Assert.Equal("remote-1234", posts[0].GetProperty("remoteId").GetString());
    }

    private sealed class ConfigurablePublisherResolver : IPublisherResolver
    {
        private readonly IPublisher _publisher;
        public ConfigurablePublisherResolver(IPublisher publisher) => _publisher = publisher;
        public IPublisher? Resolve(string platform) => _publisher;
    }

    private sealed class ConfigurableStubPublisher : IPublisher
    {
        private readonly List<PublishedPost> _posts;
        public string Platform { get; }
        public ConfigurableStubPublisher(string platform, List<PublishedPost> posts)
        {
            Platform = platform;
            _posts = posts;
        }

        public Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
        {
            return Task.FromResult(PublishResult.Succeeded(_posts));
        }
    }

    private sealed class MockReplyAdapterResolver : IReplyPlatformAdapterResolver
    {
        private readonly IReplyPlatformAdapter _adapter;
        public MockReplyAdapterResolver(IReplyPlatformAdapter adapter) => _adapter = adapter;
        public IReplyPlatformAdapter? Resolve(string platform) => _adapter;
        public IReadOnlyCollection<string> SupportedPlatforms => new[] { "Bluesky", "MetaThreads" };
    }

    private sealed class MockReplyPlatformAdapter : IReplyPlatformAdapter
    {
        public string Platform => "Bluesky";
        public Task<IReadOnlyList<PlatformCommentItem>> FetchCommentsAsync(Post post, Account account, DateTime since, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PlatformCommentItem>>(Array.Empty<PlatformCommentItem>());

        public Task<PublishReplyResult> PublishReplyAsync(Post post, Account account, PlatformCommentItem targetComment, string replyText, CancellationToken ct = default) =>
            Task.FromResult(PublishReplyResult.Succeeded("at://did:plc:test/app.bsky.feed.post/reply123", "https://bsky.app/profile/test.bsky.social/post/reply123"));

        public Task<LikeCommentResult> LikeCommentAsync(Post post, Account account, PlatformCommentItem targetComment, CancellationToken ct = default) =>
            Task.FromResult(LikeCommentResult.Succeeded("like-123"));
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



