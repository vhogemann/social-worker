using System;
using System.Collections.Generic;
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
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Features.Scheduling.Services;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Tests.Infrastructure;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Mcp;

public sealed class McpServerTests : SqliteTestBase
{
    private (McpServer Server, AppUser User, AppDbContext Db) CreateTestServer(
        TestBackgroundTaskQueue? customQueue = null,
        IReplyPlatformAdapterResolver? customAdapterResolver = null)
    {
        var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var sourcesService = TestServiceFactory.CreateSourcesService(db);
        var storage = new FileStorageProvider(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        var draftsService = TestServiceFactory.CreateDraftsService(db, storage, sourcesService);
        var taskQueue = customQueue ?? new TestBackgroundTaskQueue();
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
        var feedDiscoveryService = new FeedDiscoveryService(new HttpClient());
        var defaultAdapterResolver = customAdapterResolver ?? new MockReplyAdapterResolver(new MockReplyPlatformAdapter());

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
            replyAdapterResolver: defaultAdapterResolver);

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



