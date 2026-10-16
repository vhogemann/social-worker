using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.CodeImages;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.ImageEngine;
using SocialWorker.Api.Features.Mcp.Models;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.PlatformVariants;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Features.ReplyAgents.Services;
using SocialWorker.Api.Features.Scheduling.Models;
using SocialWorker.Api.Features.Scheduling.Services;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Features.Workflows;
using SocialWorker.Api.Features.Workflows.Abstractions;
using SocialWorker.Api.Infrastructure.Platforms;
using SocialWorker.Api.Infrastructure.Search;

namespace SocialWorker.Api.Features.Mcp.Services;

public sealed class McpToolRegistry
{
    private readonly AppDbContext _db;
    private readonly DraftsService _draftsService;
    private readonly SourcesService _sourcesService;
    private readonly FeedPollingService _feedPollingService;
    private readonly IPublisherResolver _publisherResolver;
    private readonly IDraftValidatorResolver _validatorResolver;
    private readonly BlueskyContentValidator _blueskyValidator;
    private readonly IBlueskyReplyTargetResolver _replyTargetResolver;
    private readonly IScheduleQueueService? _queueService;
    private readonly ScheduleSlotEvaluatorService? _evaluatorService;
    private readonly ICalendarProjectionService? _calendarService;
    private readonly IEnumerable<IWorkflowSourceHandler>? _sourceHandlers;
    private readonly FeedDiscoveryService? _feedDiscoveryService;
    private readonly IBackgroundTaskQueue? _taskQueue;
    private readonly ReplyAgentCoordinatorService? _replyCoordinator;
    private readonly IReplyPlatformAdapterResolver? _replyAdapterResolver;
    private readonly ReplyAgentPostService? _postService;
    private readonly IServiceProvider? _serviceProvider;
    private readonly MediaService? _mediaService;
    private readonly System.Net.Http.IHttpClientFactory? _httpClientFactory;
    private readonly CodeImageService? _codeImageService;
    private readonly ISearchEngine? _searchEngine;
    private readonly ITranscriptExtractionService? _transcriberService;
    private readonly PlatformVariantService? _platformVariantService;
    private readonly IImageEngineClient? _imageEngineClient;

    public McpToolRegistry(
        AppDbContext db,
        DraftsService draftsService,
        SourcesService sourcesService,
        FeedPollingService feedPollingService,
        IPublisherResolver publisherResolver,
        IDraftValidatorResolver validatorResolver,
        BlueskyContentValidator blueskyValidator,
        IBlueskyReplyTargetResolver replyTargetResolver,
        IScheduleQueueService? queueService = null,
        ScheduleSlotEvaluatorService? evaluatorService = null,
        ICalendarProjectionService? calendarService = null,
        IEnumerable<IWorkflowSourceHandler>? sourceHandlers = null,
        FeedDiscoveryService? feedDiscoveryService = null,
        IBackgroundTaskQueue? taskQueue = null,
        ReplyAgentCoordinatorService? replyCoordinator = null,
        IReplyPlatformAdapterResolver? replyAdapterResolver = null,
        ReplyAgentPostService? postService = null,
        IServiceProvider? serviceProvider = null,
        MediaService? mediaService = null,
        System.Net.Http.IHttpClientFactory? httpClientFactory = null,
        CodeImageService? codeImageService = null,
        ISearchEngine? searchEngine = null,
        ITranscriptExtractionService? transcriberService = null,
        PlatformVariantService? platformVariantService = null,
        IImageEngineClient? imageEngineClient = null)
    {
        _db = db;
        _draftsService = draftsService;
        _sourcesService = sourcesService;
        _feedPollingService = feedPollingService;
        _publisherResolver = publisherResolver;
        _validatorResolver = validatorResolver;
        _blueskyValidator = blueskyValidator;
        _replyTargetResolver = replyTargetResolver;
        _queueService = queueService;
        _evaluatorService = evaluatorService;
        _calendarService = calendarService;
        _sourceHandlers = sourceHandlers;
        _feedDiscoveryService = feedDiscoveryService;
        _taskQueue = taskQueue;
        _replyCoordinator = replyCoordinator;
        _replyAdapterResolver = replyAdapterResolver;
        _postService = postService;
        _serviceProvider = serviceProvider;
        _mediaService = mediaService;
        _httpClientFactory = httpClientFactory;
        _codeImageService = codeImageService;
        _searchEngine = searchEngine;
        _transcriberService = transcriberService;
        _platformVariantService = platformVariantService;
        _imageEngineClient = imageEngineClient;
    }

    private MediaService? ResolveMediaService() => _mediaService ?? _serviceProvider?.GetService<MediaService>();
    private IImageEngineClient? ResolveImageEngine() => _imageEngineClient ?? _serviceProvider?.GetService<IImageEngineClient>();
    private System.Net.Http.HttpClient ResolveHttpClient() => _httpClientFactory?.CreateClient() ?? _serviceProvider?.GetService<System.Net.Http.IHttpClientFactory>()?.CreateClient() ?? new System.Net.Http.HttpClient();
    private CodeImageService? ResolveCodeImageService() => _codeImageService ?? _serviceProvider?.GetService<CodeImageService>();
    private ISearchEngine? ResolveSearchEngine() => _searchEngine ?? _serviceProvider?.GetService<ISearchEngine>();
    private ITranscriptExtractionService? ResolveTranscriber() => _transcriberService ?? _serviceProvider?.GetService<ITranscriptExtractionService>();
    private PlatformVariantService? ResolveVariantService() => _platformVariantService ?? _serviceProvider?.GetService<PlatformVariantService>();

    public List<McpToolDefinition> ListTools()
    {
        return new List<McpToolDefinition>
        {
            new("list_drafts", "List user social media drafts with status, published posts, workflow attribution, and filtering options.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "workflowId": { "type": "string", "description": "Optional UUID of a specific workflow to filter." },
                    "isPublished": { "type": "boolean", "description": "Filter by published status (true = only published drafts)." },
                    "status": { "type": "string", "description": "Filter by draft status (Editing, Formatting, Ready, Sent, Failed)." },
                    "stage": { "type": "string", "description": "Filter by platform thread stage (Draft, Sourcing, Refining, Formatting, Ready, Sent)." },
                    "since": { "type": "string", "description": "ISO-8601 timestamp to filter drafts created/updated after this date." },
                    "limit": { "type": "integer", "description": "Maximum number of drafts to return (default 20)" }
                }
            }
            """)),
            new("get_draft", "Get full details of a draft including content, attached sources, variants, and published thread links.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The unique UUID of the draft." }
                },
                "required": ["draftId"]
            }
            """)),
            new("create_draft", "Create a new social media draft.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "title": { "type": "string", "description": "Title of the draft." },
                    "content": { "type": "string", "description": "Initial markdown content for the thread segments." },
                    "platform": { "type": "string", "description": "Optional target platform (e.g. 'Bluesky', 'Twitter', 'Threads', 'LinkedIn')." },
                    "accountId": { "type": "string", "description": "Optional connected account ID to associate with the draft." }
                },
                "required": ["title"]
            }
            """)),
            new("update_draft", "Update the title or content of an existing draft.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft to update." },
                    "title": { "type": "string", "description": "Updated title." },
                    "content": { "type": "string", "description": "Updated markdown thread content (segments separated by ---)." }
                },
                "required": ["draftId"]
            }
            """)),
            new("validate_draft", "Validate a draft's content against platform constraints, character counts, link syntax, and AST formatting.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft to validate." },
                    "platform": { "type": "string", "description": "Target platform (e.g. 'Bluesky', 'Twitter', 'Threads', 'LinkedIn'). Defaults to the draft's designated platform." }
                },
                "required": ["draftId"]
            }
            """)),
            new("delete_draft", "Permanently delete an unpublished draft and remove it from any schedule queues.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft to delete." }
                },
                "required": ["draftId"]
            }
            """)),
            new("discard_draft", "Mark an unwanted or low-relevance draft as Discarded so it will not be formatted, scheduled, or published.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft to discard." },
                    "reason": { "type": "string", "description": "Optional reason why the draft was discarded." }
                },
                "required": ["draftId"]
            }
            """)),
            new("publish_thread", "Publish a draft to a live social media platform (e.g. Bluesky, Twitter/X, Threads, LinkedIn) with server-enforced validation.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft to publish." },
                    "platform": { "type": "string", "description": "The target platform (e.g. 'Bluesky', 'Twitter', 'Threads', 'LinkedIn')." },
                    "platforms": { "type": "array", "items": { "type": "string" }, "description": "Optional list of platforms to publish to simultaneously (e.g. ['Bluesky', 'Twitter'])." },
                    "accountId": { "type": "string", "description": "Optional account ID to publish with. Required if the user has multiple accounts for this platform." },
                    "dryRun": { "type": "boolean", "description": "If true, simulates validation, account resolution, and post preview without posting to live platform." },
                    "confirm": { "type": "boolean", "description": "Confirmation flag required for irreversible live publishing actions. Defaults to true." }
                },
                "required": ["draftId"]
            }
            """)),
            new("list_sources", "List all sources (URLs, articles, video transcripts) attached to a draft.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft." }
                },
                "required": ["draftId"]
            }
            """)),
            new("add_source", "Attach an external URL or note source to a draft (automatically triggers background scraping / audio transcription).", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft." },
                    "reference": { "type": "string", "description": "URL or note content." },
                    "title": { "type": "string", "description": "Optional source title." }
                },
                "required": ["draftId", "reference"]
            }
            """)),
            new("fetch_source", "Fetch the extracted text content and summary of an attached source.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "sourceId": { "type": "string", "description": "The UUID of the source." }
                },
                "required": ["sourceId"]
            }
            """)),
            new("list_workflows", "List all automated RSS/YouTube feed workflows.", CreateSchema("""
            {
                "type": "object",
                "properties": {}
            }
            """)),
            new("trigger_workflow", "Trigger immediate feed polling and automated draft generation for a workflow.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "workflowId": { "type": "string", "description": "The UUID of the workflow to trigger." }
                },
                "required": ["workflowId"]
            }
            """)),
            new("list_feeds", "List all subscribed RSS and YouTube feeds.", CreateSchema("""
            {
                "type": "object",
                "properties": {}
            }
            """)),
            new("trigger_feed_poll", "Trigger polling on a specific feed subscription or all feeds.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "subscriptionId": { "type": "string", "description": "Optional UUID of a specific feed subscription." },
                    "forceAll": { "type": "boolean", "description": "Optional flag to force polling all items regardless of lastPolledAt." }
                }
            }
            """)),
            new("list_schedules", "List all configured publishing schedules with their timezone, active status, slot rule count, and queued draft count.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "activeOnly": { "type": "boolean", "description": "If true, only return active schedules." }
                }
            }
            """)),
            new("get_schedule", "Get full details of a specific schedule including its recurring slot rules and pending queue items.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "scheduleId": { "type": "string", "description": "The unique UUID of the schedule." }
                },
                "required": ["scheduleId"]
            }
            """)),
            new("create_schedule", "Create a new publishing schedule.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string", "description": "Name of the schedule." },
                    "timezone": { "type": "string", "description": "IANA timezone identifier (e.g. 'UTC', 'America/New_York', 'Europe/London'). Defaults to 'UTC'." }
                },
                "required": ["name"]
            }
            """)),
            new("add_slot_rule", "Add a recurring slot rule (publishing window and post capacity) to a schedule.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "scheduleId": { "type": "string", "description": "The UUID of the schedule." },
                    "recurrenceType": { "type": "string", "enum": ["Daily", "Weekly", "Monthly", "Yearly", "CustomCron"], "description": "Pattern of recurrence." },
                    "startTime": { "type": "string", "description": "Window start time in HH:mm:ss format (e.g. '09:00:00')." },
                    "endTime": { "type": "string", "description": "Window end time in HH:mm:ss format (e.g. '10:00:00'). If omitted, slot is instantaneous." },
                    "postCapacity": { "type": "integer", "description": "Number of posts to publish within this window (default 1)." },
                    "daysOfWeek": { "type": "array", "items": { "type": "integer" }, "description": "Days of week for weekly recurrence: 1=Mon, 2=Tue, 3=Wed, 4=Thu, 5=Fri, 6=Sat, 7=Sun." },
                    "dayOfMonth": { "type": "integer", "description": "Day of month (1-31) for monthly/yearly recurrence." },
                    "monthOfYear": { "type": "integer", "description": "Month of year (1-12) for yearly recurrence." },
                    "cronExpression": { "type": "string", "description": "Standard 5-part cron expression (required if recurrenceType is CustomCron)." }
                },
                "required": ["scheduleId", "recurrenceType", "startTime"]
            }
            """)),
            new("delete_slot_rule", "Delete a recurring slot rule from a schedule, or delete all slot rules if slotRuleId is omitted.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "scheduleId": { "type": "string", "description": "The unique UUID of the schedule." },
                    "slotRuleId": { "type": "string", "description": "Optional UUID of a specific slot rule to delete. If omitted, deletes all slot rules from the schedule." }
                },
                "required": ["scheduleId"]
            }
            """)),
            new("enqueue_draft", "Enqueue an existing draft into a schedule's publishing queue.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "scheduleId": { "type": "string", "description": "The UUID of the schedule." },
                    "draftId": { "type": "string", "description": "The UUID of the draft to enqueue." },
                    "platformThreadId": { "type": "string", "description": "Optional UUID of the specific platform thread variant to enqueue." },
                    "accountId": { "type": "string", "description": "Optional account ID to associate with the platform thread if not already set." },
                    "targetPosition": { "type": "integer", "description": "Optional specific 1-based queue position. Defaults to appending to end of queue." },
                    "priorityScore": { "type": "integer", "description": "Optional priority/relevance score (1-10) for dynamic queue ranking and preemption." },
                    "publishAt": { "type": "string", "description": "Optional specific future UTC timestamp (ISO 8601) to schedule publishing at without slot rules." }
                },
                "required": ["scheduleId", "draftId"]
            }
            """)),
            new("get_schedule_queue", "List the ordered queue of drafts awaiting publication in a schedule.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "scheduleId": { "type": "string", "description": "The UUID of the schedule." }
                },
                "required": ["scheduleId"]
            }
            """)),
            new("clear_schedule_queue", "Remove queued items from a schedule queue, with optional filter by workflowId.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "scheduleId": { "type": "string", "description": "The unique UUID of the schedule." },
                    "workflowId": { "type": "string", "description": "Optional UUID of a specific automation workflow to only remove items belonging to that workflow." }
                },
                "required": ["scheduleId"]
            }
            """)),
            new("get_calendar_events", "Query projected calendar slot occurrences and scheduled posts across a date-time window.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "start": { "type": "string", "description": "ISO-8601 start timestamp (e.g. '2026-09-01T00:00:00Z')." },
                    "end": { "type": "string", "description": "ISO-8601 end timestamp (e.g. '2026-09-30T23:59:59Z')." },
                    "scheduleId": { "type": "string", "description": "Optional UUID of a specific schedule to filter." }
                },
                "required": ["start", "end"]
            }
            """)),
            new("list_accounts", "List connected social media accounts (Bluesky, MetaThreads, Twitter, LinkedIn) with handles, account IDs, and status (tokens and credentials omitted).", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "platform": { "type": "string", "description": "Optional platform filter (e.g. 'MetaThreads', 'Bluesky', 'Twitter', 'LinkedIn')." }
                }
            }
            """)),
            new("discover_feed", "Discover the RSS/Atom feed URL, channel title, and website URL from any website, YouTube channel/playlist, or social profile (Instagram, Twitter/X, Telegram, Reddit, TikTok, Bluesky).", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "url": { "type": "string", "description": "The URL of the website, YouTube channel/playlist, or social profile." }
                },
                "required": ["url"]
            }
            """)),
            new("add_feed", "Subscribe to a new RSS, YouTube, or social platform feed (e.g. 'instagram://user', 'x://user', 'telegram://channel').", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "title": { "type": "string", "description": "Display title for the feed." },
                    "feedUrl": { "type": "string", "description": "Direct RSS/Atom feed URL (use discover_feed if you only have a channel or website URL)." },
                    "websiteUrl": { "type": "string", "description": "Optional website or channel URL." },
                    "includeFilters": { "type": "string", "description": "Optional comma-separated keywords that items must contain." },
                    "excludeFilters": { "type": "string", "description": "Optional comma-separated keywords that items must not contain." }
                },
                "required": ["title", "feedUrl"]
            }
            """)),
            new("update_feed", "Update an existing RSS or YouTube feed subscription's title, URLs, or include/exclude filters.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "subscriptionId": { "type": "string", "description": "The unique UUID of the feed subscription to update." },
                    "title": { "type": "string", "description": "Optional display title for the feed." },
                    "feedUrl": { "type": "string", "description": "Optional direct RSS/Atom feed URL." },
                    "websiteUrl": { "type": "string", "description": "Optional website or channel URL." },
                    "includeFilters": { "type": "string", "description": "Optional comma-separated keywords that items must contain." },
                    "excludeFilters": { "type": "string", "description": "Optional comma-separated keywords that items must not contain." }
                },
                "required": ["subscriptionId"]
            }
            """)),
            new("create_workflow", "Create a new automated publishing workflow connecting feed sources, instruction prompts, and account destinations.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "title": { "type": "string", "description": "Title of the workflow." },
                    "instructionPrompt": { "type": "string", "description": "Instructions for LLM thread generation from feed items (include platform tone, YouTube embed rules, character limits)." },
                    "feedSubscriptionIds": {
                        "type": "array",
                        "items": { "type": "string" },
                        "description": "List of feed subscription UUIDs to ingest as sources."
                    },
                    "destinations": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "platform": { "type": "string", "description": "Target platform (e.g. 'MetaThreads', 'Bluesky', 'Twitter', 'LinkedIn')." },
                                "accountId": { "type": "string", "description": "Connected account UUID (from list_accounts)." },
                                "publishingMode": { "type": "string", "enum": ["Manual", "Automatic", "Scheduled"], "description": "Publishing mode (default: Manual)." },
                                "autoPublish": { "type": "boolean", "description": "Set true to automatically publish when items are ingested (shortcut for publishingMode=Automatic)." },
                                "scheduleId": { "type": "string", "description": "Schedule UUID if publishingMode is Scheduled." }
                            },
                            "required": ["platform"]
                        },
                        "description": "Target publishing destinations for generated threads."
                    },
                    "scheduleId": { "type": "string", "description": "Optional schedule UUID for workflow-level scheduling." },
                    "minRelevanceScore": { "type": "integer", "description": "Minimum impact/relevance score (1-10) for feed items to become drafts (default: 7, 0 to disable)." }
                },
                "required": ["title"]
            }
            """)),
            new("update_workflow", "Update an existing automated publishing workflow's title, instruction prompt, feed sources, destinations, or schedule.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "workflowId": { "type": "string", "description": "The unique UUID of the workflow to update." },
                    "title": { "type": "string", "description": "Optional updated title of the workflow." },
                    "instructionPrompt": { "type": "string", "description": "Optional updated instruction prompt." },
                    "feedSubscriptionIds": {
                        "type": "array",
                        "items": { "type": "string" },
                        "description": "Updated list of feed subscription UUIDs to ingest as sources."
                    },
                    "destinations": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "platform": { "type": "string", "description": "Target platform (e.g. 'MetaThreads', 'Bluesky', 'Twitter', 'LinkedIn')." },
                                "accountId": { "type": "string", "description": "Connected account UUID (from list_accounts)." },
                                "publishingMode": { "type": "string", "enum": ["Manual", "Automatic", "Scheduled"], "description": "Publishing mode (default: Manual)." },
                                "autoPublish": { "type": "boolean", "description": "Set true to automatically publish when items are ingested." },
                                "scheduleId": { "type": "string", "description": "Schedule UUID if publishingMode is Scheduled." }
                            },
                            "required": ["platform"]
                        },
                        "description": "Updated target publishing destinations for generated threads."
                    },
                    "scheduleId": { "type": "string", "description": "Optional schedule UUID for workflow-level scheduling." },
                    "minRelevanceScore": { "type": "integer", "description": "Minimum impact/relevance score (1-10) for feed items to become drafts (default: 7, 0 to disable)." },
                    "status": { "type": "string", "enum": ["Active", "Paused", "Archived"], "description": "Workflow lifecycle state." }
                },
                "required": ["workflowId"]
            }
            """)),
            new("delete_workflow", "Delete an automated publishing workflow.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "workflowId": { "type": "string", "description": "The unique UUID of the workflow to delete." }
                },
                "required": ["workflowId"]
            }
            """)),
            new("list_reply_agents", "List all autonomous reply agents in the user's library with assignment status and summary stats.", CreateSchema("""
            {
                "type": "object",
                "properties": {}
            }
            """)),
            new("create_reply_agent", "Create a new autonomous reply agent in the user's library.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "name": { "type": "string", "description": "The name of the reply agent." },
                    "description": { "type": "string", "description": "Optional description of what this reply agent is used for." },
                    "isEnabled": { "type": "boolean", "description": "Enable or disable the reply agent (default: true)." },
                    "personaPrompt": { "type": "string", "description": "Prompt defining persona, tone, guidelines, and rules for generating replies." },
                    "minRankingScore": { "type": "integer", "description": "Minimum comment quality score (1-10) required to reply (default: 7)." },
                    "maxRepliesPerCommentThread": { "type": "integer", "description": "Maximum replies the agent can post in a single thread branch (default: 3)." },
                    "maxRepliesPerUser": { "type": "integer", "description": "Maximum replies the agent will post to any single user in the thread (default: 2)." },
                    "maxThreadDepth": { "type": "integer", "description": "Maximum comment tree depth to reply to (default: 3)." },
                    "maxCommentAgeHours": { "type": "integer", "description": "Ignore comments older than this age in hours (default: 24)." },
                    "maxPostAgeDays": { "type": "integer", "description": "Only monitor comments on posts published within the last N days (default: 7)." },
                    "autoPublishReplies": { "type": "boolean", "description": "Automatically post replies (true) or hold as PendingApproval for manual review (false). Default: true." },
                    "checkIntervalMinutes": { "type": "integer", "description": "Polling interval in minutes (default: 15)." }
                },
                "required": ["name"]
            }
            """)),
            new("update_reply_agent", "Update settings and prompt configuration for an autonomous reply agent.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "agentId": { "type": "string", "description": "The unique UUID of the reply agent." },
                    "name": { "type": "string", "description": "The updated name of the reply agent." },
                    "description": { "type": "string", "description": "Optional description of what this reply agent is used for." },
                    "isEnabled": { "type": "boolean", "description": "Enable or disable the reply agent." },
                    "personaPrompt": { "type": "string", "description": "Prompt defining persona, tone, guidelines, and rules for generating replies." },
                    "minRankingScore": { "type": "integer", "description": "Minimum comment quality score (1-10) required to reply." },
                    "maxRepliesPerCommentThread": { "type": "integer", "description": "Maximum replies the agent can post in a single thread branch." },
                    "maxRepliesPerUser": { "type": "integer", "description": "Maximum replies the agent will post to any single user in the thread." },
                    "maxThreadDepth": { "type": "integer", "description": "Maximum comment tree depth to reply to." },
                    "maxCommentAgeHours": { "type": "integer", "description": "Ignore comments older than this age in hours." },
                    "maxPostAgeDays": { "type": "integer", "description": "Only monitor comments on posts published within the last N days." },
                    "autoPublishReplies": { "type": "boolean", "description": "Automatically post replies (true) or hold as PendingApproval for manual review (false)." },
                    "checkIntervalMinutes": { "type": "integer", "description": "Polling interval in minutes." }
                },
                "required": ["agentId", "name"]
            }
            """)),
            new("assign_reply_agent", "Assign or unassign an autonomous reply agent to/from an automation workflow.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "workflowId": { "type": "string", "description": "The unique UUID of the automation workflow." },
                    "agentId": { "type": "string", "description": "The unique UUID of the reply agent to assign, or null/empty to unassign." }
                },
                "required": ["workflowId"]
            }
            """)),
            new("configure_reply_agent", "Create, enable, or update the autonomous reply agent configuration for an automation workflow.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "workflowId": { "type": "string", "description": "The unique UUID of the automation workflow." },
                    "isEnabled": { "type": "boolean", "description": "Enable or disable the reply agent (default: true)." },
                    "personaPrompt": { "type": "string", "description": "Prompt defining persona, tone, guidelines, and rules for generating replies." },
                    "minRankingScore": { "type": "integer", "description": "Minimum comment quality score (1-10) required to reply (default: 7)." },
                    "maxRepliesPerCommentThread": { "type": "integer", "description": "Maximum replies the agent can post in a single thread branch (default: 3)." },
                    "maxRepliesPerUser": { "type": "integer", "description": "Maximum replies the agent will post to any single user in the thread (default: 2)." },
                    "maxThreadDepth": { "type": "integer", "description": "Maximum comment tree depth to reply to (default: 3)." },
                    "maxCommentAgeHours": { "type": "integer", "description": "Ignore comments older than this age in hours (default: 24)." },
                    "maxPostAgeDays": { "type": "integer", "description": "Only monitor comments on posts published within the last N days (default: 7)." },
                    "autoPublishReplies": { "type": "boolean", "description": "Automatically post replies (true) or hold as PendingApproval for manual review (false). Default: true." },
                    "checkIntervalMinutes": { "type": "integer", "description": "Polling interval in minutes (default: 15)." }
                },
                "required": ["workflowId"]
            }
            """)),
            new("get_reply_agent", "Get the autonomous reply agent configuration, settings, assigned workflows, and activity statistics for a reply agent (by agentId) or for an automation workflow (by workflowId).", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "agentId": { "type": "string", "description": "The unique UUID of the reply agent (optional if workflowId is provided)." },
                    "workflowId": { "type": "string", "description": "The unique UUID of the automation workflow (optional if agentId is provided)." }
                }
            }
            """)),
            new("trigger_reply_check", "Trigger comment evaluation and replies for a workflow or reply agent. Can run synchronously to immediately return evaluation results or asynchronously in background.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "workflowId": { "type": "string", "description": "The unique UUID of the automation workflow (optional if agentId is provided)." },
                    "agentId": { "type": "string", "description": "The unique UUID of the reply agent (optional if workflowId is provided)." },
                    "postId": { "type": "string", "description": "Optional UUID of a specific post to check immediately." },
                    "runSynchronously": { "type": "boolean", "description": "If true, executes immediately and returns evaluated comments and replies. If false, queues background check tasks (default: false)." }
                }
            }
            """)),
            new("list_comment_audits", "List audited comments seen by a workflow's reply agent, including LLM ranking scores, decisions, reasoning, and generated replies.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "workflowId": { "type": "string", "description": "The unique UUID of the automation workflow (optional if agentId is provided)." },
                    "agentId": { "type": "string", "description": "The unique UUID of the reply agent (optional if workflowId is provided)." },
                    "decision": { "type": "string", "description": "Optional filter by decision: 'Replied', 'SkippedBelowThreshold', 'SkippedStopCriteria', 'PendingApproval', 'Failed'." },
                    "limit": { "type": "integer", "description": "Maximum number of audit records to return (default: 20)." },
                    "offset": { "type": "integer", "description": "Number of audit records to skip for pagination (default: 0)." }
                }
            }
            """)),
            new("reply_to_comment", "Send or approve a reply to a comment. Can approve/send a pending reply from a comment audit ID, or post a custom reply text directly to a comment on Bluesky or Meta Threads.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "auditId": { "type": "string", "description": "UUID of an existing comment audit (e.g. with PendingApproval decision) to approve and publish." },
                    "workflowId": { "type": "string", "description": "UUID of the workflow (required if auditId is not provided)." },
                    "postId": { "type": "string", "description": "UUID of post being commented on (required if auditId is not provided)." },
                    "commentId": { "type": "string", "description": "Remote platform ID of the comment being replied to (required if auditId is not provided)." },
                    "authorHandle": { "type": "string", "description": "Author handle of the comment (optional, used if auditId is not provided)." },
                    "replyText": { "type": "string", "description": "Reply text to publish. If omitted and auditId is provided, the generated reply from the audit is used." }
                }
            }
            """)),
            new("list_posts", "List published social media posts across platforms (Bluesky, Twitter/X, MetaThreads, LinkedIn) with reply agent audited comment counts, remote IDs, and workflow attribution to inspect performance and engagement.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "platform": { "type": "string", "description": "Filter by platform (e.g. 'Bluesky', 'Twitter', 'MetaThreads', 'LinkedIn')." },
                    "workflowId": { "type": "string", "description": "Filter by automation workflow UUID." },
                    "since": { "type": "string", "description": "ISO-8601 timestamp to filter posts published after this date." },
                    "hasReplies": { "type": "boolean", "description": "Filter to only posts that have received comments or replies." },
                    "limit": { "type": "integer", "description": "Maximum number of posts to return (default 20, max 100)." }
                }
            }
            """)),
            new("assign_reply_agent_post", "Assign an organic, external, or internal published post to an autonomous reply agent to actively monitor comments and generate/publish replies.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "agentId": { "type": "string", "description": "The unique UUID of the reply agent." },
                    "url": { "type": "string", "description": "Public URL of the post (e.g. Bluesky post URL, Meta Threads post URL)." },
                    "remoteId": { "type": "string", "description": "Remote ID or URI of the post if URL is not provided (e.g. at://did:plc:.../app.bsky.feed.post/...)." },
                    "platform": { "type": "string", "description": "Platform hint ('Bluesky', 'MetaThreads', 'Twitter', 'LinkedIn')." },
                    "postId": { "type": "string", "description": "Internal UUID of an existing post already in the system." }
                },
                "required": ["agentId"]
            }
            """)),
            new("unassign_reply_agent_post", "Unassign and stop monitoring a post from an autonomous reply agent.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "agentId": { "type": "string", "description": "The unique UUID of the reply agent." },
                    "postId": { "type": "string", "description": "The UUID of the post to unassign." }
                },
                "required": ["agentId", "postId"]
            }
            """)),
            new("list_reply_agent_posts", "List posts assigned to an autonomous reply agent or all monitored posts with comment and reply metrics.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "agentId": { "type": "string", "description": "Optional UUID of the reply agent to filter posts." },
                    "activeOnly": { "type": "boolean", "description": "Filter to only active assignments (default: true)." }
                }
            }
            """)),
            new("import_media_from_url", "Download an image from an external URL, process and optimize it, and attach it to a social media draft. Returns markdown media tag e.g. ![alt](media://{id}).", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft to attach media to." },
                    "url": { "type": "string", "description": "The absolute HTTP/HTTPS URL of the image to download." },
                    "altText": { "type": "string", "description": "Optional accessibility alt text for the image." }
                },
                "required": ["draftId", "url"]
            }
            """)),
            new("upload_media", "Upload a base64-encoded image or media file and attach it to a draft. Returns markdown media tag e.g. ![alt](media://{id}).", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft to attach media to." },
                    "fileName": { "type": "string", "description": "File name with extension (e.g. 'chart.png', 'photo.jpg')." },
                    "base64Data": { "type": "string", "description": "Base64-encoded image content (raw or data URL)." },
                    "mimeType": { "type": "string", "description": "MIME type (e.g. 'image/png', 'image/jpeg', 'image/webp')." },
                    "altText": { "type": "string", "description": "Optional accessibility alt text for the media." }
                },
                "required": ["draftId", "fileName", "base64Data"]
            }
            """)),
            new("list_templates", "Lists available visual image templates in the library along with their exact slot names, slot types, and canvas dimensions.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "search": { "type": "string", "description": "Optional keyword to filter templates by name or description (e.g. 'quote', 'metric', 'code')." }
                }
            }
            """)),
            new("render_template", "Renders an image template with custom slot parameters (text, quotes, metrics, background image) and attaches the generated graphic to a draft.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft to attach the rendered template to." },
                    "templateId": { "type": "string", "description": "The exact GUID or exact Name of the template to render." },
                    "slots": { "type": "object", "description": "Key-value mapping of slot names to text values or media://{guid} references.", "additionalProperties": { "type": "string" } },
                    "altText": { "type": "string", "description": "Optional accessibility alt text for preview and publishing." }
                },
                "required": ["draftId", "templateId", "slots"]
            }
            """)),
            new("render_code_blocks", "Renders code fences in a draft into Carbon-style syntax-highlighted images and replaces the code blocks with compact media markdown references.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft containing code fences." },
                    "theme": { "type": "string", "enum": ["Dark", "Light"], "description": "Visual color theme for code snippets. Defaults to Dark." },
                    "blockIndex": { "type": "integer", "description": "Optional 0-based index of a specific code block to render. If omitted, renders all blocks." }
                },
                "required": ["draftId"]
            }
            """)),
            new("search_images", "Search the web for images based on a query and return candidate URLs for visual inspection and importing.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "query": { "type": "string", "description": "The search term or keyword to find images for." }
                },
                "required": ["query"]
            }
            """)),
            new("update_media", "Update accessibility alt text for an existing media asset.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "mediaId": { "type": "string", "description": "The UUID of the media asset." },
                    "altText": { "type": "string", "description": "Updated accessibility alt text." }
                },
                "required": ["mediaId", "altText"]
            }
            """)),
            new("delete_media", "Delete an attached media asset by ID.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "mediaId": { "type": "string", "description": "The UUID of the media asset to delete." }
                },
                "required": ["mediaId"]
            }
            """)),
            new("schedule_post", "Schedule a draft thread to be published at a specific future UTC timestamp without requiring manual slot rules.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft to schedule." },
                    "publishAt": { "type": "string", "description": "ISO 8601 UTC date-time string when the post should be published (must be in future)." },
                    "platform": { "type": "string", "description": "Optional target platform (e.g. 'Bluesky', 'Twitter', 'Threads', 'LinkedIn')." },
                    "accountId": { "type": "string", "description": "Optional account ID to publish with." },
                    "scheduleId": { "type": "string", "description": "Optional schedule ID. If omitted, uses active user schedule or creates default." }
                },
                "required": ["draftId", "publishAt"]
            }
            """)),
            new("remove_queue_item", "Remove a scheduled post item from a publishing queue.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "scheduleId": { "type": "string", "description": "The UUID of the schedule." },
                    "queueItemId": { "type": "string", "description": "The UUID of the queue item to remove." }
                },
                "required": ["scheduleId", "queueItemId"]
            }
            """)),
            new("reorder_queue", "Reorder pending queue items within a schedule by supplying the full list of item IDs in desired order.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "scheduleId": { "type": "string", "description": "The UUID of the schedule." },
                    "itemIdsInOrder": { "type": "array", "items": { "type": "string" }, "description": "Ordered list of queue item UUIDs." }
                },
                "required": ["scheduleId", "itemIdsInOrder"]
            }
            """)),
            new("pause_queue_item", "Pause or resume a specific item in the schedule queue.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "scheduleId": { "type": "string", "description": "The UUID of the schedule." },
                    "queueItemId": { "type": "string", "description": "The UUID of the queue item." },
                    "isPaused": { "type": "boolean", "description": "True to pause, false to resume." }
                },
                "required": ["scheduleId", "queueItemId", "isPaused"]
            }
            """)),
            new("retry_queue_item", "Retry a failed or expired scheduled queue item.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "scheduleId": { "type": "string", "description": "The UUID of the schedule." },
                    "queueItemId": { "type": "string", "description": "The UUID of the queue item to retry." }
                },
                "required": ["queueItemId"]
            }
            """)),
            new("update_schedule", "Update an existing publishing schedule's name, timezone, or active state.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "scheduleId": { "type": "string", "description": "The UUID of the schedule to update." },
                    "name": { "type": "string", "description": "Updated name for the schedule." },
                    "timezone": { "type": "string", "description": "Updated IANA timezone (e.g. 'UTC', 'America/New_York')." },
                    "isActive": { "type": "boolean", "description": "Whether the schedule is active." }
                },
                "required": ["scheduleId"]
            }
            """)),
            new("delete_schedule", "Delete a publishing schedule and its associated slot rules and queue items.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "scheduleId": { "type": "string", "description": "The UUID of the schedule to delete." }
                },
                "required": ["scheduleId"]
            }
            """)),
            new("delete_feed", "Delete a feed subscription by ID.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "feedId": { "type": "string", "description": "The UUID of the feed subscription to delete." }
                },
                "required": ["feedId"]
            }
            """)),
            new("delete_reply_agent", "Delete a reply agent by ID.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "agentId": { "type": "string", "description": "The UUID of the reply agent to delete." }
                },
                "required": ["agentId"]
            }
            """)),
            new("get_workflow", "Get complete details and status of an automation workflow by ID.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "workflowId": { "type": "string", "description": "The UUID of the workflow." }
                },
                "required": ["workflowId"]
            }
            """)),
            new("generate_variants", "Generate platform-specific adaptations of a canonical draft for target social platforms (Bluesky, Twitter, Threads, LinkedIn).", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft to adapt." },
                    "platforms": { "type": "array", "items": { "type": "string" }, "description": "Target platforms (e.g. ['Bluesky', 'Twitter', 'MetaThreads', 'LinkedIn'])." }
                },
                "required": ["draftId"]
            }
            """)),
            new("set_bluesky_reply_target", "Configure a draft as a reply to a specific Bluesky post URL, fetching root and parent thread references.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft to attach reply target to." },
                    "replyParentUrl": { "type": "string", "description": "The full Bluesky post URL to reply to (e.g. 'https://bsky.app/profile/.../post/...')." }
                },
                "required": ["draftId", "replyParentUrl"]
            }
            """)),
            new("transcribe_video", "Extract audio and transcribe speech from a video URL (e.g. YouTube) using the transcription service.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "url": { "type": "string", "description": "URL of the video to transcribe." }
                },
                "required": ["url"]
            }
            """)),
            new("web_search", "Search the web for sources, references, or latest information given a query.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "query": { "type": "string", "description": "The search query string." }
                },
                "required": ["query"]
            }
            """))
        };
    }

    public async Task<McpToolCallResult> ExecuteToolAsync(
        string toolName,
        JsonElement? arguments,
        Guid userId,
        CancellationToken ct)
    {
        try
        {
            var args = arguments ?? JsonDocument.Parse("{}").RootElement;
            return toolName switch
            {
                "list_drafts" => await HandleListDraftsAsync(userId, args, ct),
                "get_draft" => await HandleGetDraftAsync(userId, args, ct),
                "create_draft" => await HandleCreateDraftAsync(userId, args, ct),
                "update_draft" => await HandleUpdateDraftAsync(userId, args, ct),
                "delete_draft" => await HandleDeleteDraftAsync(userId, args, ct),
                "discard_draft" => await HandleDiscardDraftAsync(userId, args, ct),
                "validate_draft" => await HandleValidateDraftAsync(userId, args, ct),
                "publish_thread" => await HandlePublishThreadAsync(userId, args, ct),
                "list_sources" => await HandleListSourcesAsync(userId, args, ct),
                "add_source" => await HandleAddSourceAsync(userId, args, ct),
                "fetch_source" => await HandleFetchSourceAsync(userId, args, ct),
                "list_workflows" => await HandleListWorkflowsAsync(userId, ct),
                "trigger_workflow" => await HandleTriggerWorkflowAsync(userId, args, ct),
                "list_feeds" => await HandleListFeedsAsync(userId, ct),
                "trigger_feed_poll" => await HandleTriggerFeedPollAsync(userId, args, ct),
                "list_schedules" => await HandleListSchedulesAsync(userId, args, ct),
                "get_schedule" => await HandleGetScheduleAsync(userId, args, ct),
                "create_schedule" => await HandleCreateScheduleAsync(userId, args, ct),
                "add_slot_rule" => await HandleAddSlotRuleAsync(userId, args, ct),
                "delete_slot_rule" => await HandleDeleteSlotRuleAsync(userId, args, ct),
                "enqueue_draft" => await HandleEnqueueDraftAsync(userId, args, ct),
                "get_schedule_queue" => await HandleGetScheduleQueueAsync(userId, args, ct),
                "clear_schedule_queue" => await HandleClearScheduleQueueAsync(userId, args, ct),
                "get_calendar_events" => await HandleGetCalendarEventsAsync(userId, args, ct),
                "list_accounts" => await HandleListAccountsAsync(userId, args, ct),
                "discover_feed" => await HandleDiscoverFeedAsync(args, ct),
                "add_feed" => await HandleAddFeedAsync(userId, args, ct),
                "update_feed" => await HandleUpdateFeedAsync(userId, args, ct),
                "create_workflow" => await HandleCreateWorkflowAsync(userId, args, ct),
                "update_workflow" => await HandleUpdateWorkflowAsync(userId, args, ct),
                "delete_workflow" => await HandleDeleteWorkflowAsync(userId, args, ct),
                "list_reply_agents" => await HandleListReplyAgentsAsync(userId, ct),
                "create_reply_agent" => await HandleCreateReplyAgentAsync(userId, args, ct),
                "update_reply_agent" => await HandleUpdateReplyAgentAsync(userId, args, ct),
                "assign_reply_agent" => await HandleAssignReplyAgentAsync(userId, args, ct),
                "get_reply_agent" => await HandleGetReplyAgentAsync(userId, args, ct),
                "configure_reply_agent" => await HandleConfigureReplyAgentAsync(userId, args, ct),
                "trigger_reply_check" => await HandleTriggerReplyCheckAsync(userId, args, ct),
                "list_comment_audits" => await HandleListCommentAuditsAsync(userId, args, ct),
                "reply_to_comment" => await HandleReplyToCommentAsync(userId, args, ct),
                "list_posts" => await HandleListPostsAsync(userId, args, ct),
                "assign_reply_agent_post" => await HandleAssignReplyAgentPostAsync(userId, args, ct),
                "unassign_reply_agent_post" => await HandleUnassignReplyAgentPostAsync(userId, args, ct),
                "list_reply_agent_posts" => await HandleListReplyAgentPostsAsync(userId, args, ct),
                "import_media_from_url" => await HandleImportMediaFromUrlAsync(userId, args, ct),
                "upload_media" => await HandleUploadMediaAsync(userId, args, ct),
                "list_templates" => await HandleListTemplatesAsync(userId, args, ct),
                "render_template" => await HandleRenderTemplateAsync(userId, args, ct),
                "render_code_blocks" => await HandleRenderCodeBlocksAsync(userId, args, ct),
                "search_images" => await HandleSearchImagesAsync(args, ct),
                "update_media" => await HandleUpdateMediaAsync(userId, args, ct),
                "delete_media" => await HandleDeleteMediaAsync(userId, args, ct),
                "schedule_post" => await HandleSchedulePostAsync(userId, args, ct),
                "remove_queue_item" => await HandleRemoveQueueItemAsync(userId, args, ct),
                "reorder_queue" => await HandleReorderQueueAsync(userId, args, ct),
                "pause_queue_item" => await HandlePauseQueueItemAsync(userId, args, ct),
                "retry_queue_item" => await HandleRetryQueueItemAsync(userId, args, ct),
                "update_schedule" => await HandleUpdateScheduleAsync(userId, args, ct),
                "delete_schedule" => await HandleDeleteScheduleAsync(userId, args, ct),
                "delete_feed" => await HandleDeleteFeedAsync(userId, args, ct),
                "delete_reply_agent" => await HandleDeleteReplyAgentAsync(userId, args, ct),
                "get_workflow" => await HandleGetWorkflowAsync(userId, args, ct),
                "generate_variants" => await HandleGenerateVariantsAsync(userId, args, ct),
                "set_bluesky_reply_target" => await HandleSetBlueskyReplyTargetAsync(userId, args, ct),
                "transcribe_video" => await HandleTranscribeVideoAsync(args, ct),
                "web_search" => await HandleWebSearchAsync(args, ct),
                _ => TextError($"Unknown tool '{toolName}'.")
            };
        }
        catch (Exception ex)
        {
            return TextError($"Tool execution failed: {ex.Message}");
        }
    }

    private async Task<McpToolCallResult> HandleListDraftsAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        var limit = args.TryGetProperty("limit", out var l) && l.TryGetInt32(out var lim) ? lim : 20;

        Guid? workflowId = null;
        if (args.TryGetProperty("workflowId", out var wfProp) && Guid.TryParse(wfProp.GetString(), out var parsedWf))
        {
            workflowId = parsedWf;
        }

        bool? isPublished = null;
        if (args.TryGetProperty("isPublished", out var pubProp) && pubProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            isPublished = pubProp.GetBoolean();
        }

        DraftStatus? status = null;
        if (args.TryGetProperty("status", out var stProp) && Enum.TryParse<DraftStatus>(stProp.GetString(), true, out var parsedStatus))
        {
            status = parsedStatus;
        }

        PlatformThreadStage? stage = null;
        if (args.TryGetProperty("stage", out var stageProp) && Enum.TryParse<PlatformThreadStage>(stageProp.GetString(), true, out var parsedStage))
        {
            stage = parsedStage;
        }

        DateTime? since = null;
        if (args.TryGetProperty("since", out var sinceProp) && DateTime.TryParse(sinceProp.GetString(), out var parsedSince))
        {
            since = parsedSince;
        }

        var filter = new DraftQueryFilter(
            WorkflowId: workflowId,
            IsPublished: isPublished,
            Status: status,
            Stage: stage,
            Since: since,
            Limit: limit
        );

        var drafts = await _draftsService.GetDraftsForUserAsync(userId, filter, ct);
        var selected = drafts.Select(d =>
        {
            var isPub = (d.Threads != null && d.Threads.Any(t => t.Stage == "Sent")) || (d.Variants != null && d.Variants.Any(v => v.Threads != null && v.Threads.Any(vt => vt.Stage == "Sent")));
            var variantPosts = d.Variants != null ? d.Variants.SelectMany(v => v.Threads != null ? v.Threads.SelectMany(vt => vt.Posts ?? Enumerable.Empty<PostDto>()) : Enumerable.Empty<PostDto>()) : Enumerable.Empty<PostDto>();
            var urls = (d.Threads != null ? d.Threads.SelectMany(t => t.Posts ?? Enumerable.Empty<PostDto>()) : Enumerable.Empty<PostDto>())
                .Concat(variantPosts)
                .Where(p => !string.IsNullOrEmpty(p.Url))
                .Select(p => p.Url!)
                .Distinct()
                .ToList();

            return new
            {
                d.Id,
                d.Title,
                d.Status,
                d.TargetPlatform,
                d.AutomationWorkflowId,
                d.AutomationWorkflowTitle,
                d.DiscardReason,
                d.RelevanceScore,
                IsPublished = isPub,
                PublishedUrls = urls,
                d.UpdatedAt
            };
        });
        return TextResult(JsonSerializer.Serialize(selected, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleGetDraftAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var draftId))
        {
            return TextError("draftId must be a valid UUID.");
        }

        var draft = await _draftsService.GetDraftByIdAsync(userId, draftId, ct);
        var mediaWithTags = draft.MediaAssets?.Select(m => new
        {
            m.Id,
            m.FileName,
            m.MimeType,
            m.AltText,
            m.SizeBytes,
            m.Width,
            m.Height,
            m.CreatedAt,
            markdownTag = $"![{m.AltText ?? m.FileName}](media://{m.Id})"
        }).ToList();

        var result = new
        {
            draft.Id,
            draft.Title,
            draft.Status,
            draft.Content,
            draft.TargetPlatform,
            draft.CanonicalDraftId,
            draft.Threads,
            MediaAssets = mediaWithTags,
            draft.BlueskyReplyTarget,
            draft.ChatHistory,
            draft.ChatSummary,
            draft.LastSummarizedMessageCount,
            draft.CreatedAt,
            draft.UpdatedAt,
            draft.TargetAccountId,
            draft.TargetAccountHandle,
            draft.AutomationWorkflowId,
            draft.AutomationWorkflowTitle,
            draft.Variants,
            draft.DiscardReason,
            draft.RelevanceScore
        };

        return TextResult(JsonSerializer.Serialize(result, JsonOptions));
    }


    private async Task<McpToolCallResult> HandleCreateDraftAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        var title = args.GetProperty("title").GetString() ?? "New Draft";
        var content = args.TryGetProperty("content", out var c) ? c.GetString() : null;
        var platform = args.TryGetProperty("platform", out var p) ? p.GetString() : null;
        Guid? accountId = null;
        if (args.TryGetProperty("accountId", out var accProp) && Guid.TryParse(accProp.GetString(), out var parsedAccId))
        {
            accountId = parsedAccId;
        }

        var result = await _draftsService.CreateDraftAsync(userId, title, content, platform, accountId, ct);
        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            id = result.Id,
            title = result.Title,
            platform = result.TargetPlatform,
            message = $"Draft created successfully with ID: {result.Id}"
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleUpdateDraftAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var draftId))
        {
            return TextError("draftId must be a valid UUID.");
        }

        var title = args.TryGetProperty("title", out var t) ? t.GetString() : null;
        var content = args.TryGetProperty("content", out var c) ? c.GetString() : null;

        var result = await _draftsService.UpdateDraftAsync(userId, draftId, title, content, null, null, null, null, ct);
        return TextResult($"Draft {result.Id} updated successfully.");
    }

    private async Task<McpToolCallResult> HandleDeleteDraftAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var draftId))
        {
            return TextError("draftId must be a valid UUID.");
        }

        var deleted = await _draftsService.DeleteDraftAsync(userId, draftId, ct);
        if (!deleted)
        {
            return TextError($"Draft '{draftId}' not found or could not be deleted.");
        }

        return TextResult($"Draft '{draftId}' successfully deleted.");
    }

    private async Task<McpToolCallResult> HandleDiscardDraftAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var draftId))
        {
            return TextError("draftId must be a valid UUID.");
        }

        var reason = args.TryGetProperty("reason", out var rProp) ? rProp.GetString() : "Manually discarded by user/agent.";
        var discarded = await _draftsService.DiscardDraftAsync(userId, draftId, reason, ct);
        if (!discarded)
        {
            return TextError($"Draft '{draftId}' not found or could not be discarded.");
        }

        return TextResult($"Draft '{draftId}' successfully marked as Discarded.");
    }

    private async Task<McpToolCallResult> HandleValidateDraftAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var draftId))
        {
            return TextError("draftId must be a valid UUID.");
        }

        string? platform = args.TryGetProperty("platform", out var p) ? p.GetString() : null;
        if (string.IsNullOrWhiteSpace(platform))
        {
            var draft = await _db.Drafts.Include(d => d.Threads).FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId, ct);
            if (draft != null)
            {
                platform = draft.Threads.FirstOrDefault()?.Platform ?? draft.TargetPlatform?.ToString();
            }
            platform ??= "Bluesky";
        }

        var validatorTool = new ValidateDraftTool(_db, _validatorResolver);
        var validation = await validatorTool.ExecuteAsync(new ValidateDraftArgs(null, platform), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draftId));
        return TextResult($"Platform: {platform}\n\n{validation.ToDisplayText()}");
    }

    private async Task<McpToolCallResult> HandlePublishThreadAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var draftId))
        {
            return TextError("draftId must be a valid UUID.");
        }

        bool isDryRun = args.TryGetProperty("dryRun", out var drProp) && drProp.ValueKind == JsonValueKind.True;
        bool confirm = !args.TryGetProperty("confirm", out var cfProp) || cfProp.ValueKind != JsonValueKind.False;

        if (!confirm && !isDryRun)
        {
            return TextError("Publishing is an irreversible public action. Please specify confirm: true or use dryRun: true to preview.");
        }

        var platforms = new List<string>();
        if (args.TryGetProperty("platforms", out var platsProp) && platsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in platsProp.EnumerateArray())
            {
                if (!string.IsNullOrWhiteSpace(el.GetString()))
                    platforms.Add(el.GetString()!);
            }
        }
        else if (args.TryGetProperty("platform", out var pProp) && !string.IsNullOrWhiteSpace(pProp.GetString()))
        {
            platforms.Add(pProp.GetString()!);
        }

        if (platforms.Count == 0)
        {
            var draft = await _db.Drafts.Include(d => d.Threads).FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId, ct);
            if (draft != null)
            {
                var candidate = draft.Threads.FirstOrDefault()?.Platform ?? draft.TargetPlatform?.ToString();
                if (!string.IsNullOrWhiteSpace(candidate))
                    platforms.Add(candidate);
            }
            if (platforms.Count == 0)
                platforms.Add("Bluesky");
        }

        Guid? accountId = null;
        if (args.TryGetProperty("accountId", out var accProp) && Guid.TryParse(accProp.GetString(), out var parsedAccId))
        {
            accountId = parsedAccId;
        }

        var publishTool = new PublishPlatformTool(_db, _publisherResolver, _blueskyValidator, _replyTargetResolver, _validatorResolver);
        var validatorTool = new ValidateDraftTool(_db, _validatorResolver);

        if (isDryRun)
        {
            var previews = new List<object>();
            foreach (var plat in platforms)
            {
                var matchingAccounts = await _db.Accounts
                    .Where(a => a.UserId == userId && a.Platform.ToLower() == plat.ToLower())
                    .ToListAsync(ct);

                var resolvedAccount = accountId.HasValue
                    ? matchingAccounts.FirstOrDefault(a => a.Id == accountId.Value)
                    : (matchingAccounts.Count == 1 ? matchingAccounts[0] : null);

                var validation = await validatorTool.ExecuteAsync(
                    new ValidateDraftArgs(null, plat),
                    SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draftId));

                previews.Add(new
                {
                    platform = plat,
                    accountHandle = resolvedAccount?.Handle,
                    accountId = resolvedAccount?.Id,
                    multipleAccounts = matchingAccounts.Count > 1,
                    availableAccounts = matchingAccounts.Select(a => new { a.Id, a.Handle }).ToList(),
                    isValid = !validation.HasBlockingErrors,
                    overallStatus = validation.OverallStatus.ToString(),
                    posts = validation.Posts.Select(p => new
                    {
                        postNumber = p.PostNumber,
                        characterCount = p.CharacterCount,
                        imageCount = p.ImageCount,
                        issues = p.Issues.Select(i => new { severity = i.Severity.ToString(), message = i.Message })
                    })
                });
            }

            return TextResult(JsonSerializer.Serialize(new
            {
                dryRun = true,
                draftId,
                platforms,
                previews,
                message = "Dry-run preview and validation completed. No posts were published."
            }, JsonOptions));
        }

        var results = new List<object>();
        foreach (var plat in platforms)
        {
            var result = await publishTool.ExecuteAsync(
                new PublishPlatformArgs(plat, accountId),
                SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draftId));

            if (!result.Success)
            {
                return TextError(result.Error ?? result.Message);
            }

            results.Add(new
            {
                platform = plat,
                success = result.Success,
                message = result.Message,
                posts = result.Posts
            });
        }

        if (results.Count == 1)
        {
            var single = (dynamic)results[0];
            return TextResult(single.message);
        }

        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            draftId,
            publishedPlatforms = results,
            message = $"Successfully published draft to {results.Count} platforms."
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleListSourcesAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var draftId))
        {
            return TextError("draftId must be a valid UUID.");
        }

        var sources = await _sourcesService.GetSourcesForDraftAsync(userId, draftId, ct);
        return TextResult(JsonSerializer.Serialize(sources, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleAddSourceAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var draftId))
        {
            return TextError("draftId must be a valid UUID.");
        }

        var reference = args.TryGetProperty("reference", out var r) ? r.GetString()
            : (args.TryGetProperty("url", out var u) ? u.GetString() : null);

        if (string.IsNullOrWhiteSpace(reference))
        {
            return TextError("Either 'reference' or 'url' is required.");
        }

        var title = args.TryGetProperty("title", out var t) ? t.GetString() : null;

        var result = await _sourcesService.AddUrlSourceAsync(userId, draftId, reference, title, null, ct);
        return TextResult($"Source added successfully with ID: {result.SourceId}");
    }

    private async Task<McpToolCallResult> HandleFetchSourceAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("sourceId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var sourceId))
        {
            return TextError("sourceId must be a valid UUID.");
        }

        var source = await _db.Sources.FirstOrDefaultAsync(s => s.Id == sourceId, ct);
        if (source == null)
        {
            return TextError($"Source {sourceId} not found.");
        }

        return TextResult($"Title: {source.Title}\nKind: {source.Kind}\nReference: {source.Reference}\nSummary: {source.Summary}\n\nContent:\n{source.Content}");
    }

    private async Task<McpToolCallResult> HandleListWorkflowsAsync(Guid userId, CancellationToken ct)
    {
        var workflows = await _db.AutomationWorkflows
            .AsNoTracking()
            .Where(w => w.UserId == userId)
            .Include(w => w.WorkflowSources)
                .ThenInclude(ws => ws.FeedSubscription)
            .Include(w => w.Destinations)
                .ThenInclude(d => d.Account)
            .Include(w => w.Destinations)
                .ThenInclude(d => d.Schedule)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

        var list = workflows.Select(w => new
        {
            w.Id,
            w.Title,
            w.InstructionPrompt,
            status = "Active",
            sources = w.WorkflowSources.Select(s => new WorkflowSourceDto(s)).ToList(),
            destinations = w.Destinations.Select(d => new WorkflowDestinationDto(d)).ToList(),
            w.ScheduleId,
            ScheduleName = w.Schedule?.Name,
            w.MinRelevanceScore,
            w.CreatedAt,
            w.UpdatedAt
        }).ToList();
        return TextResult(JsonSerializer.Serialize(list, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleTriggerWorkflowAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("workflowId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var workflowId))
        {
            return TextError("workflowId must be a valid UUID.");
        }

        var workflow = await _db.AutomationWorkflows
            .Include(w => w.WorkflowSources)
            .Include(w => w.Destinations)
                .ThenInclude(d => d.Account)
            .Include(w => w.Destinations)
                .ThenInclude(d => d.Schedule)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

        if (workflow == null)
        {
            return TextError($"Workflow {workflowId} not found.");
        }

        var handlersList = (_sourceHandlers ?? Array.Empty<IWorkflowSourceHandler>()).ToList();
        var processedCount = 0;
        foreach (var source in workflow.WorkflowSources)
        {
            var handler = handlersList.FirstOrDefault(h => h.Kind == source.Kind);
            if (handler != null)
            {
                await handler.ProcessAsync(workflow, source, ct);
                processedCount++;
            }
            else if (source.Kind == WorkflowSourceKind.Feed && source.FeedSubscriptionId.HasValue)
            {
                await _feedPollingService.PollSubscriptionAsync(source.FeedSubscriptionId.Value, ct);
                processedCount++;
            }
        }

        return TextResult($"Triggered workflow {workflowId} across {processedCount} source(s).");
    }

    private async Task<McpToolCallResult> HandleListFeedsAsync(Guid userId, CancellationToken ct)
    {
        var feeds = await _db.FeedSubscriptions
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => new { f.Id, f.Title, f.FeedUrl, f.WebsiteUrl, f.IncludeFilters, f.ExcludeFilters, f.LastPolledAt })
            .ToListAsync(ct);

        return TextResult(JsonSerializer.Serialize(feeds, JsonOptions));
    }


    private async Task<McpToolCallResult> HandleTriggerFeedPollAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        var forceAll = args.TryGetProperty("forceAll", out var f) && f.GetBoolean();
        if (args.TryGetProperty("subscriptionId", out var idProp) && Guid.TryParse(idProp.GetString(), out var subId))
        {
            await _feedPollingService.PollSubscriptionAsync(subId, ct, forceAll);
            return TextResult($"Polled feed subscription {subId}.");
        }

        await _feedPollingService.PollAllSubscriptionsAsync(ct);
        return TextResult("Polled all subscribed feeds.");
    }

    private async Task<McpToolCallResult> HandleListSchedulesAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        var activeOnly = args.TryGetProperty("activeOnly", out var a) && a.GetBoolean();
        var query = _db.Schedules
            .AsNoTracking()
            .Where(s => s.UserId == userId);

        if (activeOnly)
        {
            query = query.Where(s => s.IsActive);
        }

        var schedules = await query
            .Include(s => s.SlotRules)
            .Include(s => s.QueueItems)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var dtos = schedules.Select(s => new ScheduleSummaryDto(s)).ToList();
        return TextResult(JsonSerializer.Serialize(dtos, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleGetScheduleAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("scheduleId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var scheduleId))
        {
            return TextError("scheduleId must be a valid UUID.");
        }

        var schedule = await _db.Schedules
            .AsNoTracking()
            .Where(s => s.Id == scheduleId && s.UserId == userId)
            .Include(s => s.SlotRules)
            .Include(s => s.QueueItems.Where(q => q.Status != ScheduleQueueItemStatus.Succeeded))
            .FirstOrDefaultAsync(ct);

        if (schedule == null)
        {
            return TextError($"Schedule {scheduleId} not found.");
        }

        return TextResult(JsonSerializer.Serialize(new ScheduleDetailDto(schedule), JsonOptions));
    }

    private async Task<McpToolCallResult> HandleCreateScheduleAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("name", out var nameProp) || string.IsNullOrWhiteSpace(nameProp.GetString()))
        {
            return TextError("Schedule name is required.");
        }

        var name = nameProp.GetString()!.Trim();
        var timezone = args.TryGetProperty("timezone", out var tzProp) ? tzProp.GetString() : "UTC";

        var schedule = Schedule.Create(userId, name, timezone);
        _db.Schedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        return TextResult(JsonSerializer.Serialize(new ScheduleSummaryDto(schedule), JsonOptions));
    }

    private async Task<McpToolCallResult> HandleAddSlotRuleAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("scheduleId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var scheduleId))
        {
            return TextError("scheduleId must be a valid UUID.");
        }

        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.Id == scheduleId && s.UserId == userId, ct);
        if (schedule == null)
        {
            return TextError($"Schedule {scheduleId} not found.");
        }

        if (!args.TryGetProperty("recurrenceType", out var recProp) ||
            !Enum.TryParse<SlotRecurrenceType>(recProp.GetString(), true, out var recurrenceType))
        {
            return TextError("recurrenceType must be one of: Daily, Weekly, Monthly, Yearly, CustomCron.");
        }

        if (!args.TryGetProperty("startTime", out var startProp) ||
            !TimeOnly.TryParse(startProp.GetString(), out var startTime))
        {
            return TextError("startTime must be in valid HH:mm:ss format.");
        }

        TimeOnly? endTime = null;
        if (args.TryGetProperty("endTime", out var endProp) && !string.IsNullOrWhiteSpace(endProp.GetString()))
        {
            if (TimeOnly.TryParse(endProp.GetString(), out var parsedEnd))
            {
                endTime = parsedEnd;
            }
            else
            {
                return TextError("endTime must be in valid HH:mm:ss format.");
            }
        }

        var postCapacity = 1;
        if (args.TryGetProperty("postCapacity", out var capProp) && capProp.GetInt32() > 0)
        {
            postCapacity = capProp.GetInt32();
        }

        int[]? daysOfWeek = null;
        if (args.TryGetProperty("daysOfWeek", out var dowProp) && dowProp.ValueKind == JsonValueKind.Array)
        {
            daysOfWeek = dowProp.EnumerateArray().Select(e => e.GetInt32()).ToArray();
        }

        int? dayOfMonth = args.TryGetProperty("dayOfMonth", out var domProp) ? domProp.GetInt32() : null;
        int? monthOfYear = args.TryGetProperty("monthOfYear", out var moyProp) ? moyProp.GetInt32() : null;

        string cron = args.TryGetProperty("cronExpression", out var cronProp) ? cronProp.GetString()?.Trim() ?? "" : "";
        if (string.IsNullOrWhiteSpace(cron))
        {
            cron = recurrenceType switch
            {
                SlotRecurrenceType.Daily => CronExpressionBuilder.Daily(startTime),
                SlotRecurrenceType.Weekly => CronExpressionBuilder.Weekly(startTime, daysOfWeek ?? new[] { 1, 2, 3, 4, 5 }),
                SlotRecurrenceType.Monthly => CronExpressionBuilder.Monthly(startTime, dayOfMonth ?? 1),
                SlotRecurrenceType.Yearly => CronExpressionBuilder.Yearly(startTime, monthOfYear ?? 1, dayOfMonth ?? 1),
                _ => ""
            };
        }

        if (!CronExpressionBuilder.TryValidate(cron, out var cronError))
        {
            return TextError($"Invalid cron expression: {cronError}");
        }

        var resolvedEndTime = endTime ?? startTime;
        if (resolvedEndTime < startTime)
        {
            return TextError("endTime cannot be earlier than startTime.");
        }

        var rule = ScheduleSlotRule.Create(
            scheduleId,
            recurrenceType,
            cron,
            startTime,
            resolvedEndTime,
            postCapacity,
            daysOfWeek,
            dayOfMonth,
            monthOfYear
        );

        _db.ScheduleSlotRules.Add(rule);
        await _db.SaveChangesAsync(ct);

        if (_evaluatorService != null)
        {
            await _evaluatorService.EvaluateScheduleAsync(scheduleId, ct: ct);
        }

        return TextResult(JsonSerializer.Serialize(new ScheduleSlotRuleDto(rule), JsonOptions));
    }

    private async Task<McpToolCallResult> HandleDeleteSlotRuleAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("scheduleId", out var sProp) || !Guid.TryParse(sProp.GetString(), out var scheduleId))
        {
            return TextError("'scheduleId' is required and must be a valid UUID.");
        }

        var schedule = await _db.Schedules
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.UserId == userId, ct);

        if (schedule == null)
        {
            return TextError($"Schedule '{scheduleId}' not found.");
        }

        if (args.TryGetProperty("slotRuleId", out var srProp) && Guid.TryParse(srProp.GetString(), out var slotRuleId))
        {
            var rule = await _db.ScheduleSlotRules
                .FirstOrDefaultAsync(r => r.Id == slotRuleId && r.ScheduleId == scheduleId, ct);

            if (rule == null)
            {
                return TextError($"Slot rule '{slotRuleId}' not found.");
            }

            _db.ScheduleSlotRules.Remove(rule);
            await _db.SaveChangesAsync(ct);

            if (_evaluatorService != null)
            {
                await _evaluatorService.EvaluateScheduleAsync(scheduleId, ct: ct);
            }

            return TextResult($"Slot rule '{slotRuleId}' deleted successfully.");
        }
        else
        {
            var rules = await _db.ScheduleSlotRules
                .Where(r => r.ScheduleId == scheduleId)
                .ToListAsync(ct);

            _db.ScheduleSlotRules.RemoveRange(rules);
            await _db.SaveChangesAsync(ct);

            if (_evaluatorService != null)
            {
                await _evaluatorService.EvaluateScheduleAsync(scheduleId, ct: ct);
            }

            return TextResult($"Deleted all {rules.Count} slot rules from schedule '{scheduleId}'.");
        }
    }

    private async Task<McpToolCallResult> HandleEnqueueDraftAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("scheduleId", out var sProp) || !Guid.TryParse(sProp.GetString(), out var scheduleId))
        {
            return TextError("scheduleId must be a valid UUID.");
        }

        if (!args.TryGetProperty("draftId", out var dProp) || !Guid.TryParse(dProp.GetString(), out var draftId))
        {
            return TextError("draftId must be a valid UUID.");
        }

        var exists = await _db.Schedules.AnyAsync(s => s.Id == scheduleId && s.UserId == userId, ct);
        if (!exists)
        {
            return TextError($"Schedule {scheduleId} not found.");
        }

        var draft = await _db.Drafts
            .Include(d => d.Threads)
            .FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId, ct);

        if (draft == null)
        {
            return TextError($"Draft {draftId} not found.");
        }

        if (_queueService == null)
        {
            return TextError("Schedule queue service is unavailable.");
        }

        Guid? accountId = null;
        if (args.TryGetProperty("accountId", out var accProp) && Guid.TryParse(accProp.GetString(), out var parsedAccId))
        {
            accountId = parsedAccId;
        }

        Guid platformThreadId;
        if (args.TryGetProperty("platformThreadId", out var ptProp) && Guid.TryParse(ptProp.GetString(), out var parsedPtId))
        {
            platformThreadId = parsedPtId;
        }
        else
        {
            var existingThread = draft.Threads.FirstOrDefault();
            if (existingThread != null)
            {
                if (accountId.HasValue && !existingThread.AccountId.HasValue)
                {
                    existingThread.AccountId = accountId.Value;
                    await _db.SaveChangesAsync(ct);
                }
                platformThreadId = existingThread.Id;
            }
            else
            {
                var newThread = new PlatformThread
                {
                    Id = Guid.NewGuid(),
                    DraftId = draftId,
                    AccountId = accountId,
                    Platform = draft.TargetPlatform?.ToString() ?? "Bluesky",
                    Stage = PlatformThreadStage.Draft,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.PlatformThreads.Add(newThread);
                await _db.SaveChangesAsync(ct);
                platformThreadId = newThread.Id;
            }
        }

        int? targetPosition = args.TryGetProperty("targetPosition", out var posProp) ? posProp.GetInt32() : null;
        int? priorityScore = args.TryGetProperty("priorityScore", out var prioProp)
            ? prioProp.GetInt32()
            : (args.TryGetProperty("priority_score", out var prioScoreProp) ? prioScoreProp.GetInt32() : null);

        var item = await _queueService.EnqueueAsync(scheduleId, platformThreadId, draftId, targetPosition, priorityScore, ct);

        if (args.TryGetProperty("publishAt", out var paProp) && DateTime.TryParse(paProp.GetString(), out var publishAt))
        {
            var publishAtUtc = publishAt.ToUniversalTime();
            if (_taskQueue != null && publishAtUtc > DateTime.UtcNow)
            {
                var delay = publishAtUtc - DateTime.UtcNow;
                var payload = new ScheduledPublishingPayload(item.Id, scheduleId, draftId, platformThreadId);
                var taskId = await _taskQueue.EnqueueAsync(
                    "scheduled-publishing",
                    payload,
                    new EnqueueTaskOptions { Delay = delay, DeduplicationKey = $"publish:{item.Id}" },
                    ct);

                item.Status = ScheduleQueueItemStatus.Dispatched;
                item.AssignedSlotUtc = publishAtUtc;
                item.ScheduledTaskId = taskId;
                item.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }
        else if (_evaluatorService != null)
        {
            await _evaluatorService.EvaluateScheduleAsync(scheduleId, ct: ct);
        }

        return TextResult(JsonSerializer.Serialize(new ScheduleQueueItemDto(item), JsonOptions));
    }

    private async Task<McpToolCallResult> HandleGetScheduleQueueAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("scheduleId", out var sProp) || !Guid.TryParse(sProp.GetString(), out var scheduleId))
        {
            return TextError("scheduleId must be a valid UUID.");
        }

        var exists = await _db.Schedules.AnyAsync(s => s.Id == scheduleId && s.UserId == userId, ct);
        if (!exists)
        {
            return TextError($"Schedule {scheduleId} not found.");
        }

        if (_queueService == null)
        {
            return TextError("Schedule queue service is unavailable.");
        }

        var queue = await _queueService.GetQueueAsync(scheduleId, ct);

        return TextResult(JsonSerializer.Serialize(queue.Select(q => new ScheduleQueueItemDto(q)).ToList(), JsonOptions));
    }

    private async Task<McpToolCallResult> HandleClearScheduleQueueAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("scheduleId", out var sProp) || !Guid.TryParse(sProp.GetString(), out var scheduleId))
        {
            return TextError("scheduleId must be a valid UUID.");
        }

        var exists = await _db.Schedules.AnyAsync(s => s.Id == scheduleId && s.UserId == userId, ct);
        if (!exists)
        {
            return TextError($"Schedule {scheduleId} not found.");
        }

        if (_queueService == null)
        {
            return TextError("Schedule queue service is unavailable.");
        }

        Guid? workflowId = null;
        if (args.TryGetProperty("workflowId", out var wfProp) && Guid.TryParse(wfProp.GetString(), out var parsedWf))
        {
            workflowId = parsedWf;
        }

        if (workflowId.HasValue)
        {
            var count = await _queueService.RemoveWorkflowItemsFromQueueAsync(scheduleId, workflowId.Value, ct);
            return TextResult($"Removed {count} queue items for workflow '{workflowId.Value}' from schedule '{scheduleId}'.");
        }
        else
        {
            var items = await _queueService.GetQueueAsync(scheduleId, ct);
            int count = 0;
            foreach (var item in items)
            {
                if (await _queueService.RemoveFromQueueAsync(scheduleId, item.Id, ct))
                {
                    count++;
                }
            }
            return TextResult($"Removed all {count} queue items from schedule '{scheduleId}'.");
        }
    }

    private async Task<McpToolCallResult> HandleGetCalendarEventsAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("start", out var startProp) || !DateTime.TryParse(startProp.GetString(), out var startUtc))
        {
            return TextError("start must be a valid ISO-8601 timestamp.");
        }

        if (!args.TryGetProperty("end", out var endProp) || !DateTime.TryParse(endProp.GetString(), out var endUtc))
        {
            return TextError("end must be a valid ISO-8601 timestamp.");
        }

        Guid? scheduleId = null;
        if (args.TryGetProperty("scheduleId", out var sProp) && Guid.TryParse(sProp.GetString(), out var parsedId))
        {
            scheduleId = parsedId;
        }

        var calendarService = _calendarService ?? new CalendarProjectionService(_db);
        try
        {
            var events = await calendarService.GetCalendarEventsAsync(userId, startUtc.ToUniversalTime(), endUtc.ToUniversalTime(), scheduleId, ct);
            return TextResult(JsonSerializer.Serialize(events, JsonOptions));
        }
        catch (ArgumentException ex)
        {
            return TextError(ex.Message);
        }
    }

    private async Task<McpToolCallResult> HandleListAccountsAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        var query = _db.Accounts.AsNoTracking().Where(a => a.UserId == userId);

        if (args.TryGetProperty("platform", out var pProp) && !string.IsNullOrWhiteSpace(pProp.GetString()))
        {
            var platformInput = pProp.GetString()!.Trim();
            if (SocialPlatformParser.TryParse(platformInput, out var parsedPlatform))
            {
                var platformStr = parsedPlatform.ToString();
                query = query.Where(a => a.Platform == platformStr || a.Platform == platformInput);
            }
            else
            {
                query = query.Where(a => a.Platform == platformInput);
            }
        }

        var accounts = await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                id = a.Id,
                platform = a.Platform,
                handle = a.Handle,
                status = a.Status,
                createdAt = a.CreatedAt
            })
            .ToListAsync(ct);

        return TextResult(JsonSerializer.Serialize(accounts, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleDiscoverFeedAsync(JsonElement args, CancellationToken ct)
    {
        if (_feedDiscoveryService == null)
        {
            return TextError("Feed discovery service is not configured.");
        }

        if (!args.TryGetProperty("url", out var urlProp) || string.IsNullOrWhiteSpace(urlProp.GetString()))
        {
            return TextError("url is required.");
        }

        var url = urlProp.GetString()!.Trim();
        var result = await _feedDiscoveryService.DiscoverAsync(url);
        if (!result.Success)
        {
            return TextError(result.Error ?? "Failed to discover feed.");
        }

        return TextResult(JsonSerializer.Serialize(new
        {
            feedUrl = result.FeedUrl,
            title = result.Title,
            websiteUrl = result.WebsiteUrl,
            success = result.Success
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleAddFeedAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("title", out var titleProp) || string.IsNullOrWhiteSpace(titleProp.GetString()))
        {
            return TextError("title is required.");
        }
        if (!args.TryGetProperty("feedUrl", out var feedUrlProp) || string.IsNullOrWhiteSpace(feedUrlProp.GetString()))
        {
            return TextError("feedUrl is required.");
        }

        var title = titleProp.GetString()!.Trim();
        var feedUrl = feedUrlProp.GetString()!.Trim();
        var websiteUrl = args.TryGetProperty("websiteUrl", out var wProp) ? wProp.GetString()?.Trim() : null;
        var includeFilters = args.TryGetProperty("includeFilters", out var iProp) ? iProp.GetString()?.Trim() : null;
        var excludeFilters = args.TryGetProperty("excludeFilters", out var eProp) ? eProp.GetString()?.Trim() : null;

        var req = new CreateFeedSubscriptionRequest(title, feedUrl, websiteUrl, includeFilters, excludeFilters);
        var subscription = FeedSubscription.Create(userId, req);

        _db.FeedSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);

        return TextResult(JsonSerializer.Serialize(new FeedSubscriptionDto(subscription), JsonOptions));
    }

    private async Task<McpToolCallResult> HandleUpdateFeedAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("subscriptionId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var subscriptionId))
        {
            return TextError("subscriptionId is required and must be a valid UUID.");
        }

        var subscription = await _db.FeedSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.UserId == userId, ct);

        if (subscription == null)
        {
            return TextError("Feed subscription not found.");
        }

        if (args.TryGetProperty("title", out var titleProp) && !string.IsNullOrWhiteSpace(titleProp.GetString()))
        {
            subscription.Title = titleProp.GetString()!.Trim();
        }

        if (args.TryGetProperty("feedUrl", out var feedUrlProp) && !string.IsNullOrWhiteSpace(feedUrlProp.GetString()))
        {
            subscription.FeedUrl = feedUrlProp.GetString()!.Trim();
        }

        if (args.TryGetProperty("websiteUrl", out var wProp))
        {
            subscription.WebsiteUrl = wProp.GetString()?.Trim();
        }

        if (args.TryGetProperty("includeFilters", out var iProp))
        {
            subscription.IncludeFilters = iProp.GetString()?.Trim();
        }

        if (args.TryGetProperty("excludeFilters", out var eProp))
        {
            subscription.ExcludeFilters = eProp.GetString()?.Trim();
        }

        await _db.SaveChangesAsync(ct);
        return TextResult(JsonSerializer.Serialize(new FeedSubscriptionDto(subscription), JsonOptions));
    }

    private async Task<McpToolCallResult> HandleCreateWorkflowAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("title", out var titleProp) || string.IsNullOrWhiteSpace(titleProp.GetString()))
        {
            return TextError("title is required.");
        }

        var title = titleProp.GetString()!.Trim();
        var instructionPrompt = args.TryGetProperty("instructionPrompt", out var instProp) ? instProp.GetString() : null;

        Guid? scheduleId = null;
        if (args.TryGetProperty("scheduleId", out var sProp) && Guid.TryParse(sProp.GetString(), out var parsedSched))
        {
            scheduleId = parsedSched;
        }

        int minRelevanceScore = 7;
        if (args.TryGetProperty("minRelevanceScore", out var mrsProp) && mrsProp.TryGetInt32(out var parsedMrs))
        {
            minRelevanceScore = Math.Clamp(parsedMrs, 0, 10);
        }

        var workflow = AutomationWorkflow.Create(userId, title, instructionPrompt, scheduleId, minRelevanceScore);
        _db.AutomationWorkflows.Add(workflow);

        if (args.TryGetProperty("feedSubscriptionIds", out var feedsArray) && feedsArray.ValueKind == JsonValueKind.Array)
        {
            var requestedFeedIds = new List<Guid>();
            foreach (var item in feedsArray.EnumerateArray())
            {
                if (Guid.TryParse(item.GetString(), out var feedId))
                {
                    requestedFeedIds.Add(feedId);
                }
            }

            if (requestedFeedIds.Count > 0)
            {
                var validSubIds = await _db.FeedSubscriptions
                    .Where(s => s.UserId == userId && requestedFeedIds.Contains(s.Id))
                    .Select(s => s.Id)
                    .ToListAsync(ct);

                foreach (var subId in validSubIds)
                {
                    workflow.WorkflowSources.Add(AutomationWorkflowSource.CreateFeedSource(workflow.Id, subId));
                }
            }
        }

        if (args.TryGetProperty("destinations", out var destArray) && destArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var destEl in destArray.EnumerateArray())
            {
                var platform = destEl.TryGetProperty("platform", out var pProp) ? pProp.GetString()?.Trim() : null;
                if (string.IsNullOrWhiteSpace(platform)) continue;

                var autoPub = destEl.TryGetProperty("autoPublish", out var apProp) && apProp.GetBoolean();
                var modeStr = destEl.TryGetProperty("publishingMode", out var pmProp) ? pmProp.GetString() : null;
                var mode = Enum.TryParse<WorkflowPublishingMode>(modeStr, true, out var parsedMode)
                    ? parsedMode
                    : (autoPub ? WorkflowPublishingMode.Automatic : WorkflowPublishingMode.Manual);

                Guid? destSchedId = null;
                if (destEl.TryGetProperty("scheduleId", out var dsProp) && Guid.TryParse(dsProp.GetString(), out var parsedDs))
                {
                    destSchedId = parsedDs;
                }

                Guid? destAccountId = null;
                if (destEl.TryGetProperty("accountId", out var daProp) && Guid.TryParse(daProp.GetString(), out var parsedDa))
                {
                    destAccountId = parsedDa;
                }

                workflow.Destinations.Add(AutomationWorkflowDestination.Create(workflow.Id, platform, mode, destSchedId, destAccountId));
            }
        }

        await _db.SaveChangesAsync(ct);

        var created = await _db.AutomationWorkflows
            .Include(w => w.Schedule)
            .Include(w => w.WorkflowSources)
                .ThenInclude(ws => ws.FeedSubscription)
            .Include(w => w.Destinations)
                .ThenInclude(d => d.Account)
            .Include(w => w.Destinations)
                .ThenInclude(d => d.Schedule)
            .FirstAsync(w => w.Id == workflow.Id, ct);

        return TextResult(JsonSerializer.Serialize(new AutomationWorkflowDto(created), JsonOptions));
    }

    private async Task<McpToolCallResult> HandleUpdateWorkflowAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("workflowId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var workflowId))
        {
            return TextError("workflowId is required and must be a valid UUID.");
        }

        var workflow = await _db.AutomationWorkflows
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

        if (workflow == null)
        {
            return TextError("Workflow not found.");
        }

        if (args.TryGetProperty("title", out var titleProp) && !string.IsNullOrWhiteSpace(titleProp.GetString()))
        {
            workflow.Title = titleProp.GetString()!.Trim();
        }

        if (args.TryGetProperty("instructionPrompt", out var instProp))
        {
            workflow.InstructionPrompt = instProp.GetString() ?? "";
        }

        if (args.TryGetProperty("scheduleId", out var sProp))
        {
            workflow.ScheduleId = Guid.TryParse(sProp.GetString(), out var parsedSched) ? parsedSched : null;
        }

        if (args.TryGetProperty("minRelevanceScore", out var mrsProp) && mrsProp.TryGetInt32(out var parsedMrs))
        {
            workflow.MinRelevanceScore = Math.Clamp(parsedMrs, 0, 10);
        }

        workflow.UpdatedAt = DateTime.UtcNow;

        if (args.TryGetProperty("feedSubscriptionIds", out var feedsArray) && feedsArray.ValueKind == JsonValueKind.Array)
        {
            var requestedFeedIds = new List<Guid>();
            foreach (var item in feedsArray.EnumerateArray())
            {
                if (Guid.TryParse(item.GetString(), out var feedId))
                {
                    requestedFeedIds.Add(feedId);
                }
            }

            var existingSources = await _db.AutomationWorkflowSources
                .Where(s => s.AutomationWorkflowId == workflowId)
                .ToListAsync(ct);
            _db.AutomationWorkflowSources.RemoveRange(existingSources);

            if (requestedFeedIds.Count > 0)
            {
                var validSubIds = await _db.FeedSubscriptions
                    .Where(s => s.UserId == userId && requestedFeedIds.Contains(s.Id))
                    .Select(s => s.Id)
                    .ToListAsync(ct);

                foreach (var subId in validSubIds)
                {
                    _db.AutomationWorkflowSources.Add(AutomationWorkflowSource.CreateFeedSource(workflow.Id, subId));
                }
            }
        }

        if (args.TryGetProperty("destinations", out var destArray) && destArray.ValueKind == JsonValueKind.Array)
        {
            var existingDestinations = await _db.AutomationWorkflowDestinations
                .Where(d => d.AutomationWorkflowId == workflowId)
                .ToListAsync(ct);
            _db.AutomationWorkflowDestinations.RemoveRange(existingDestinations);

            foreach (var destEl in destArray.EnumerateArray())
            {
                var platform = destEl.TryGetProperty("platform", out var pProp) ? pProp.GetString()?.Trim() : null;
                if (string.IsNullOrWhiteSpace(platform)) continue;

                var autoPub = destEl.TryGetProperty("autoPublish", out var apProp) && apProp.GetBoolean();
                var modeStr = destEl.TryGetProperty("publishingMode", out var pmProp) ? pmProp.GetString() : null;
                var mode = Enum.TryParse<WorkflowPublishingMode>(modeStr, true, out var parsedMode)
                    ? parsedMode
                    : (autoPub ? WorkflowPublishingMode.Automatic : WorkflowPublishingMode.Manual);

                Guid? destSchedId = null;
                if (destEl.TryGetProperty("scheduleId", out var dsProp) && Guid.TryParse(dsProp.GetString(), out var parsedDs))
                {
                    destSchedId = parsedDs;
                }

                Guid? destAccountId = null;
                if (destEl.TryGetProperty("accountId", out var daProp) && Guid.TryParse(daProp.GetString(), out var parsedDa))
                {
                    destAccountId = parsedDa;
                }

                _db.AutomationWorkflowDestinations.Add(AutomationWorkflowDestination.Create(workflow.Id, platform, mode, destSchedId, destAccountId));
            }
        }

        await _db.SaveChangesAsync(ct);

        var updated = await _db.AutomationWorkflows
            .Include(w => w.Schedule)
            .Include(w => w.WorkflowSources)
                .ThenInclude(ws => ws.FeedSubscription)
            .Include(w => w.Destinations)
                .ThenInclude(d => d.Account)
            .Include(w => w.Destinations)
                .ThenInclude(d => d.Schedule)
            .FirstAsync(w => w.Id == workflow.Id, ct);

        return TextResult(JsonSerializer.Serialize(new AutomationWorkflowDto(updated), JsonOptions));
    }

    private async Task<McpToolCallResult> HandleDeleteWorkflowAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("workflowId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var workflowId))
        {
            return TextError("workflowId is required and must be a valid UUID.");
        }

        var workflow = await _db.AutomationWorkflows
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

        if (workflow == null)
        {
            return TextError("Workflow not found.");
        }

        _db.AutomationWorkflows.Remove(workflow);
        await _db.SaveChangesAsync(ct);

        return TextResult($"Workflow '{workflow.Title}' ({workflowId}) successfully deleted.");
    }

    private async Task<McpToolCallResult> HandleListReplyAgentsAsync(Guid userId, CancellationToken ct)
    {
        var agents = await _db.ReplyAgents
            .Include(a => a.Workflows)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        var dtos = agents.Select(a => new ReplyAgentDto(a)).ToList();
        return TextResult(JsonSerializer.Serialize(dtos, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleCreateReplyAgentAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("name", out var nameProp) || string.IsNullOrWhiteSpace(nameProp.GetString()))
        {
            return TextError("'name' is required.");
        }

        var name = nameProp.GetString()!.Trim();
        var desc = args.TryGetProperty("description", out var dProp) ? dProp.GetString() : null;
        var personaPrompt = args.TryGetProperty("personaPrompt", out var pProp) ? pProp.GetString() : "";
        var isEnabled = !args.TryGetProperty("isEnabled", out var enProp) || enProp.ValueKind != JsonValueKind.False;
        var maxCommentAgeHours = args.TryGetProperty("maxCommentAgeHours", out var ahProp) && ahProp.TryGetInt32(out var ah) ? ah : 24;
        var maxPostAgeDays = args.TryGetProperty("maxPostAgeDays", out var pdProp) && pdProp.TryGetInt32(out var pd) ? pd : 7;
        var minRankingScore = args.TryGetProperty("minRankingScore", out var msProp) && msProp.TryGetInt32(out var ms) ? ms : 7;
        var maxRepliesPerBranch = args.TryGetProperty("maxRepliesPerCommentThread", out var mrProp) && mrProp.TryGetInt32(out var mr) ? mr : 3;
        var maxRepliesPerUser = args.TryGetProperty("maxRepliesPerUser", out var muProp) && muProp.TryGetInt32(out var mu) ? mu : 2;
        var maxThreadDepth = args.TryGetProperty("maxThreadDepth", out var tdProp) && tdProp.TryGetInt32(out var td) ? td : 3;
        var autoPublishReplies = !args.TryGetProperty("autoPublishReplies", out var apProp) || apProp.ValueKind != JsonValueKind.False;
        var checkIntervalMinutes = args.TryGetProperty("checkIntervalMinutes", out var ciProp) && ciProp.TryGetInt32(out var ci) ? ci : 15;

        var agent = ReplyAgent.Create(
            userId,
            name,
            personaPrompt,
            desc,
            maxCommentAgeHours,
            maxPostAgeDays,
            minRankingScore,
            maxRepliesPerBranch,
            maxRepliesPerUser,
            maxThreadDepth,
            autoPublishReplies,
            checkIntervalMinutes);
        agent.IsEnabled = isEnabled;

        _db.ReplyAgents.Add(agent);
        await _db.SaveChangesAsync(ct);

        return TextResult(JsonSerializer.Serialize(new ReplyAgentDto(agent), JsonOptions));
    }

    private async Task<McpToolCallResult> HandleUpdateReplyAgentAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("agentId", out var aProp) || !Guid.TryParse(aProp.GetString(), out var agentId))
        {
            return TextError("'agentId' is required and must be a valid UUID.");
        }

        var agent = await _db.ReplyAgents
            .Include(a => a.Workflows)
            .FirstOrDefaultAsync(a => a.Id == agentId && a.UserId == userId, ct);

        if (agent == null)
        {
            return TextError($"Reply Agent '{agentId}' not found.");
        }

        if (args.TryGetProperty("name", out var nameProp) && !string.IsNullOrWhiteSpace(nameProp.GetString()))
        {
            agent.Name = nameProp.GetString()!.Trim();
        }

        if (args.TryGetProperty("description", out var dProp))
        {
            agent.Description = string.IsNullOrWhiteSpace(dProp.GetString()) ? null : dProp.GetString()!.Trim();
        }

        if (args.TryGetProperty("isEnabled", out var enProp))
        {
            agent.IsEnabled = enProp.ValueKind != JsonValueKind.False;
        }

        if (args.TryGetProperty("personaPrompt", out var pProp) && pProp.GetString() != null)
        {
            agent.PersonaPrompt = pProp.GetString()!.Trim();
        }

        if (args.TryGetProperty("maxCommentAgeHours", out var ahProp) && ahProp.TryGetInt32(out var ah))
        {
            agent.MaxCommentAgeHours = Math.Max(1, ah);
        }

        if (args.TryGetProperty("maxPostAgeDays", out var pdProp) && pdProp.TryGetInt32(out var pd))
        {
            agent.MaxPostAgeDays = Math.Max(1, pd);
        }

        if (args.TryGetProperty("minRankingScore", out var msProp) && msProp.TryGetInt32(out var ms))
        {
            agent.MinRankingScore = Math.Clamp(ms, 1, 10);
        }

        if (args.TryGetProperty("maxRepliesPerCommentThread", out var mrProp) && mrProp.TryGetInt32(out var mr))
        {
            agent.MaxRepliesPerCommentThread = Math.Max(1, mr);
        }

        if (args.TryGetProperty("maxRepliesPerUser", out var muProp) && muProp.TryGetInt32(out var mu))
        {
            agent.MaxRepliesPerUser = Math.Max(1, mu);
        }

        if (args.TryGetProperty("maxThreadDepth", out var tdProp) && tdProp.TryGetInt32(out var td))
        {
            agent.MaxThreadDepth = Math.Max(1, td);
        }

        if (args.TryGetProperty("autoPublishReplies", out var apProp))
        {
            agent.AutoPublishReplies = apProp.ValueKind != JsonValueKind.False;
        }

        if (args.TryGetProperty("checkIntervalMinutes", out var ciProp) && ciProp.TryGetInt32(out var ci))
        {
            agent.CheckIntervalMinutes = Math.Max(1, ci);
        }

        agent.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return TextResult(JsonSerializer.Serialize(new ReplyAgentDto(agent), JsonOptions));
    }

    private async Task<McpToolCallResult> HandleAssignReplyAgentAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("workflowId", out var wfProp) || !Guid.TryParse(wfProp.GetString(), out var workflowId))
        {
            return TextError("'workflowId' is required and must be a valid UUID.");
        }

        var workflow = await _db.AutomationWorkflows
            .Include(w => w.ReplyAgent)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

        if (workflow == null)
        {
            return TextError("Workflow not found.");
        }

        Guid? targetAgentId = null;
        if (args.TryGetProperty("agentId", out var aProp) && !string.IsNullOrWhiteSpace(aProp.GetString()))
        {
            if (!Guid.TryParse(aProp.GetString(), out var parsedAgentId))
            {
                return TextError("'agentId' must be a valid UUID.");
            }
            targetAgentId = parsedAgentId;
        }

        if (targetAgentId.HasValue)
        {
            var agent = await _db.ReplyAgents
                .Include(a => a.Workflows)
                .FirstOrDefaultAsync(a => a.Id == targetAgentId.Value && a.UserId == userId, ct);

            if (agent == null)
            {
                return TextError($"Reply Agent '{targetAgentId.Value}' not found.");
            }

            workflow.ReplyAgentId = agent.Id;
            await _db.SaveChangesAsync(ct);

            return TextResult(JsonSerializer.Serialize(new ReplyAgentDto(agent), JsonOptions));
        }
        else
        {
            workflow.ReplyAgentId = null;
            await _db.SaveChangesAsync(ct);

            return TextResult($"Reply Agent unassigned from workflow '{workflow.Title}'.");
        }
    }

    private async Task<McpToolCallResult> HandleGetReplyAgentAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        ReplyAgent? agent = null;

        if (args.TryGetProperty("agentId", out var aProp) && Guid.TryParse(aProp.GetString(), out var agentId))
        {
            agent = await _db.ReplyAgents
                .Include(a => a.Workflows)
                .FirstOrDefaultAsync(a => a.Id == agentId && a.UserId == userId, ct);

            if (agent == null)
            {
                return TextError($"Reply Agent '{agentId}' not found.");
            }
        }
        else if (args.TryGetProperty("workflowId", out var wfProp) && Guid.TryParse(wfProp.GetString(), out var workflowId))
        {
            var workflow = await _db.AutomationWorkflows
                .Include(w => w.ReplyAgent)
                    .ThenInclude(a => a!.Workflows)
                .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

            if (workflow == null)
            {
                return TextError("Workflow not found.");
            }

            if (workflow.ReplyAgent == null)
            {
                return TextResult(JsonSerializer.Serialize(new { configured = false, workflowId }, JsonOptions));
            }

            agent = workflow.ReplyAgent;
        }
        else
        {
            return TextError("Either 'agentId' or 'workflowId' must be provided.");
        }

        var totalAudits = await _db.WorkflowCommentAudits.CountAsync(a => a.ReplyAgentId == agent.Id, ct);
        var repliedCount = await _db.WorkflowCommentAudits.CountAsync(a => a.ReplyAgentId == agent.Id && a.RankingDecision == "Replied", ct);
        var pendingCount = await _db.WorkflowCommentAudits.CountAsync(a => a.ReplyAgentId == agent.Id && a.RankingDecision == "PendingApproval", ct);

        var response = new
        {
            configured = true,
            agent = new ReplyAgentDto(agent),
            stats = new
            {
                totalAudited = totalAudits,
                repliesPublished = repliedCount,
                pendingApproval = pendingCount
            }
        };

        return TextResult(JsonSerializer.Serialize(response, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleConfigureReplyAgentAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (args.TryGetProperty("agentId", out var aProp) && Guid.TryParse(aProp.GetString(), out var agentId))
        {
            return await HandleUpdateReplyAgentAsync(userId, args, ct);
        }

        if (args.TryGetProperty("workflowId", out var wfProp) && Guid.TryParse(wfProp.GetString(), out var workflowId))
        {
            var workflow = await _db.AutomationWorkflows
                .Include(w => w.ReplyAgent)
                    .ThenInclude(a => a!.Workflows)
                .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

            if (workflow == null)
            {
                return TextError("Workflow not found.");
            }

            var agent = workflow.ReplyAgent;
            if (agent == null)
            {
                var name = $"Reply Agent for {workflow.Title}";
                var persona = args.TryGetProperty("personaPrompt", out var pp) ? pp.GetString() ?? "" : "";
                var newAgent = ReplyAgent.Create(userId, name, personaPrompt: persona);
                _db.ReplyAgents.Add(newAgent);
                workflow.ReplyAgentId = newAgent.Id;
                await _db.SaveChangesAsync(ct);
                agent = newAgent;
            }

            var updateObj = JsonSerializer.Deserialize<Dictionary<string, object>>(args.GetRawText()) ?? new();
            updateObj["agentId"] = agent.Id.ToString();
            updateObj["name"] = agent.Name;
            var updatedElement = JsonSerializer.SerializeToElement(updateObj);
            return await HandleUpdateReplyAgentAsync(userId, updatedElement, ct);
        }

        return TextError("Either 'agentId' or 'workflowId' must be provided.");
    }

    private async Task<McpToolCallResult> HandleTriggerReplyCheckAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        ReplyAgent? agent = null;
        List<Guid> workflowIds = new();

        if (args.TryGetProperty("agentId", out var aProp) && Guid.TryParse(aProp.GetString(), out var agentId))
        {
            agent = await _db.ReplyAgents
                .Include(a => a.Workflows)
                .FirstOrDefaultAsync(a => a.Id == agentId && a.UserId == userId, ct);

            if (agent == null)
            {
                return TextError($"Reply Agent '{agentId}' not found.");
            }

            workflowIds = agent.Workflows.Select(w => w.Id).ToList();
        }
        else if (args.TryGetProperty("workflowId", out var wfProp) && Guid.TryParse(wfProp.GetString(), out var workflowId))
        {
            var workflow = await _db.AutomationWorkflows
                .Include(w => w.ReplyAgent)
                .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

            if (workflow == null)
            {
                return TextError("Workflow not found.");
            }

            if (workflow.ReplyAgent == null || !workflow.ReplyAgent.IsEnabled)
            {
                return TextError("Reply Agent is not configured or disabled for this workflow.");
            }

            agent = workflow.ReplyAgent;
            workflowIds = new List<Guid> { workflow.Id };
        }
        else
        {
            return TextError("Either 'agentId' or 'workflowId' must be provided.");
        }

        if (!agent.IsEnabled)
        {
            return TextError("Reply Agent is disabled.");
        }

        var runSynchronously = args.TryGetProperty("runSynchronously", out var syncProp) && syncProp.ValueKind == JsonValueKind.True;

        Guid? specificPostId = null;
        if (args.TryGetProperty("postId", out var pProp) && Guid.TryParse(pProp.GetString(), out var parsedPid))
        {
            specificPostId = parsedPid;
        }

        var postMap = new Dictionary<Guid, Post>();

        if (workflowIds.Count > 0)
        {
            var cutoff = DateTime.UtcNow.Subtract(TimeSpan.FromDays(agent.MaxPostAgeDays));
            var workflowPosts = await _db.Posts
                .Include(p => p.Draft)
                .Where(p => p.Draft != null
                    && p.Draft.AutomationWorkflowId.HasValue
                    && workflowIds.Contains(p.Draft.AutomationWorkflowId.Value)
                    && p.RemoteId != null
                    && p.PostedAt >= cutoff)
                .ToListAsync(ct);

            foreach (var p in workflowPosts)
            {
                postMap[p.Id] = p;
            }
        }

        var assignedPosts = await _db.ReplyAgentPosts
            .Where(rap => rap.ReplyAgentId == agent.Id && rap.IsActive && rap.Post.RemoteId != null)
            .Select(rap => rap.Post)
            .ToListAsync(ct);

        foreach (var p in assignedPosts)
        {
            postMap[p.Id] = p;
        }

        var posts = postMap.Values.ToList();
        if (specificPostId.HasValue)
        {
            posts = posts.Where(p => p.Id == specificPostId.Value).ToList();
        }

        if (posts.Count == 0)
        {
            return TextResult($"No eligible published posts found for reply agent '{agent.Name}'.");
        }

        if (runSynchronously && _replyCoordinator != null)
        {
            var startTime = DateTime.UtcNow;
            foreach (var post in posts)
            {
                await _replyCoordinator.ProcessPostCommentsAsync(agent.Id, post.Id, ct);
            }

            var recentAudits = await _db.WorkflowCommentAudits
                .Where(a => a.ReplyAgentId == agent.Id && a.CreatedAt >= startTime.AddSeconds(-2))
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync(ct);

            var summary = new
            {
                postsChecked = posts.Count,
                commentsEvaluated = recentAudits.Count,
                replied = recentAudits.Count(a => a.RankingDecision == "Replied"),
                pendingApproval = recentAudits.Count(a => a.RankingDecision == "PendingApproval"),
                skipped = recentAudits.Count(a => a.RankingDecision.StartsWith("Skipped")),
                failed = recentAudits.Count(a => a.RankingDecision == "Failed"),
                audits = recentAudits.Select(a => new WorkflowCommentAuditDto(a))
            };

            return TextResult(JsonSerializer.Serialize(summary, JsonOptions));
        }

        if (_taskQueue == null)
        {
            return TextError("Background task queue is unavailable and synchronous execution was not requested.");
        }

        var enqueuedCount = 0;
        foreach (var post in posts)
        {
            var payload = new WorkflowReplyCheckPostPayload(agent.Id, post.Id);
            var dedupKey = $"manual-reply-check-{agent.Id}-{post.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            await _taskQueue.EnqueueAsync("workflow-reply-check-post", payload, EnqueueTaskOptions.WithDeduplication(dedupKey), ct);
            enqueuedCount++;
        }

        return TextResult($"Enqueued reply check for {enqueuedCount} post(s) for reply agent '{agent.Name}'.");
    }

    private async Task<McpToolCallResult> HandleListCommentAuditsAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        IQueryable<WorkflowCommentAudit> query = _db.WorkflowCommentAudits
            .Include(a => a.ReplyAgent)
            .Include(a => a.Post)
                .ThenInclude(p => p.Draft);

        if (args.TryGetProperty("agentId", out var aProp) && Guid.TryParse(aProp.GetString(), out var agentId))
        {
            var agent = await _db.ReplyAgents.FirstOrDefaultAsync(a => a.Id == agentId && a.UserId == userId, ct);
            if (agent == null) return TextError($"Reply Agent '{agentId}' not found.");

            query = query.Where(a => a.ReplyAgentId == agentId);
        }
        else if (args.TryGetProperty("workflowId", out var wfProp) && Guid.TryParse(wfProp.GetString(), out var workflowId))
        {
            var workflow = await _db.AutomationWorkflows
                .Include(w => w.ReplyAgent)
                .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

            if (workflow == null) return TextError("Workflow not found.");
            if (workflow.ReplyAgent == null) return TextResult("[]");

            query = query.Where(a => a.ReplyAgentId == workflow.ReplyAgent.Id
                && a.Post.Draft.AutomationWorkflowId == workflow.Id);
        }
        else
        {
            query = query.Where(a => a.ReplyAgent.UserId == userId);
        }

        if (args.TryGetProperty("decision", out var decProp) && !string.IsNullOrWhiteSpace(decProp.GetString()))
        {
            var dec = decProp.GetString()!;
            query = query.Where(a => a.RankingDecision == dec);
        }

        var limit = args.TryGetProperty("limit", out var lProp) && lProp.TryGetInt32(out var l) ? Math.Clamp(l, 1, 100) : 20;
        var offset = args.TryGetProperty("offset", out var oProp) && oProp.TryGetInt32(out var o) ? Math.Max(0, o) : 0;

        var audits = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

        var dtos = audits.Select(a => new WorkflowCommentAuditDto(a)).ToList();
        return TextResult(JsonSerializer.Serialize(dtos, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleReplyToCommentAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (_replyAdapterResolver == null)
        {
            return TextError("Reply platform adapter resolver is not available.");
        }

        if (args.TryGetProperty("auditId", out var auditProp) && Guid.TryParse(auditProp.GetString(), out var auditId))
        {
            var audit = await _db.WorkflowCommentAudits
                .Include(a => a.ReplyAgent)
                .Include(a => a.Post)
                    .ThenInclude(p => p.PlatformThread)
                        .ThenInclude(pt => pt.Account)
                .FirstOrDefaultAsync(a => a.Id == auditId, ct);

            if (audit == null)
            {
                return TextError($"Comment audit {auditId} not found.");
            }

            if (audit.ReplyAgent?.UserId != userId)
            {
                return TextError("Unauthorized: Comment audit does not belong to your reply agents.");
            }

            var customText = args.TryGetProperty("replyText", out var rtProp) ? rtProp.GetString() : null;
            var replyText = !string.IsNullOrWhiteSpace(customText) ? customText.Trim() : audit.GeneratedReplyText;

            if (string.IsNullOrWhiteSpace(replyText))
            {
                return TextError("No reply text provided and audit has no generated reply.");
            }

            var post = audit.Post;
            var account = post.PlatformThread?.Account;
            if (account == null)
            {
                account = await _db.Accounts.FirstOrDefaultAsync(
                    a => a.UserId == userId && a.Platform == post.Platform, ct);
            }

            if (account == null)
            {
                return TextError("Post has no associated Account for publishing.");
            }

            var adapter = _replyAdapterResolver.Resolve(post.Platform);
            if (adapter == null)
            {
                return TextError($"Platform '{post.Platform}' is not supported for replies.");
            }

            var commentItem = new PlatformCommentItem(
                Platform: audit.Platform,
                RemoteCommentId: audit.RemoteCommentId,
                RemoteParentId: audit.RemoteParentId,
                RemoteRootId: audit.RemoteRootId ?? post.RemoteId ?? audit.RemoteCommentId,
                RemoteAuthorHandle: audit.RemoteAuthorHandle,
                RemoteAuthorId: audit.RemoteAuthorId,
                Text: audit.CommentText,
                CreatedAt: audit.CommentCreatedAt,
                Depth: 1,
                ExtractedUrls: Array.Empty<string>(),
                ExtractedImages: Array.Empty<PlatformCommentImageAttachment>(),
                PlatformMetadata: null);

            var publishResult = await adapter.PublishReplyAsync(post, account, commentItem, replyText, ct);
            if (publishResult.Success)
            {
                audit.RankingDecision = "Replied";
                audit.GeneratedReplyText = replyText;
                audit.RemoteReplyId = publishResult.RemoteReplyId;
                audit.ErrorMessage = null;
                await _db.SaveChangesAsync(ct);

                return TextResult(JsonSerializer.Serialize(new
                {
                    success = true,
                    remoteReplyId = publishResult.RemoteReplyId,
                    replyUrl = publishResult.RemoteReplyUrl,
                    replyText
                }, JsonOptions));
            }
            else
            {
                audit.ErrorMessage = publishResult.ErrorMessage;
                await _db.SaveChangesAsync(ct);
                return TextError($"Failed to publish reply: {publishResult.ErrorMessage}");
            }
        }

        if (!args.TryGetProperty("postId", out var pProp) || !Guid.TryParse(pProp.GetString(), out var postId))
        {
            return TextError("Either 'auditId' or 'postId' and 'commentId' must be provided.");
        }

        if (!args.TryGetProperty("commentId", out var cProp) || string.IsNullOrWhiteSpace(cProp.GetString()))
        {
            return TextError("'commentId' is required when 'auditId' is not provided.");
        }

        var commentId = cProp.GetString()!.Trim();
        var manualReplyText = args.TryGetProperty("replyText", out var mrtProp) ? mrtProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(manualReplyText))
        {
            return TextError("'replyText' is required when replying directly.");
        }

        var manualPost = await _db.Posts
            .Include(p => p.Draft)
            .Include(p => p.PlatformThread)
                .ThenInclude(pt => pt.Account)
            .FirstOrDefaultAsync(p => p.Id == postId && p.Draft.UserId == userId, ct);

        if (manualPost == null)
        {
            return TextError($"Post {postId} not found.");
        }

        var manualAccount = manualPost.PlatformThread?.Account;
        if (manualAccount == null)
        {
            manualAccount = await _db.Accounts.FirstOrDefaultAsync(
                a => a.UserId == userId && a.Platform == manualPost.Platform, ct);
        }

        if (manualAccount == null)
        {
            return TextError("Post has no associated Account for publishing.");
        }

        var manualAdapter = _replyAdapterResolver.Resolve(manualPost.Platform);
        if (manualAdapter == null)
        {
            return TextError($"Platform '{manualPost.Platform}' is not supported for replies.");
        }

        var authorHandle = args.TryGetProperty("authorHandle", out var ahProp) ? ahProp.GetString() ?? "" : "";
        var directCommentItem = new PlatformCommentItem(
            Platform: manualPost.Platform,
            RemoteCommentId: commentId,
            RemoteParentId: commentId,
            RemoteRootId: manualPost.RemoteId ?? commentId,
            RemoteAuthorHandle: authorHandle,
            RemoteAuthorId: "",
            Text: "",
            CreatedAt: DateTime.UtcNow,
            Depth: 1,
            ExtractedUrls: Array.Empty<string>(),
            ExtractedImages: Array.Empty<PlatformCommentImageAttachment>(),
            PlatformMetadata: null);

        var directResult = await manualAdapter.PublishReplyAsync(manualPost, manualAccount, directCommentItem, manualReplyText.Trim(), ct);
        if (directResult.Success)
        {
            return TextResult(JsonSerializer.Serialize(new
            {
                success = true,
                remoteReplyId = directResult.RemoteReplyId,
                replyUrl = directResult.RemoteReplyUrl,
                replyText = manualReplyText.Trim()
            }, JsonOptions));
        }

        return TextError($"Failed to publish reply: {directResult.ErrorMessage}");
    }

    private async Task<McpToolCallResult> HandleListPostsAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        var limit = args.TryGetProperty("limit", out var l) && l.TryGetInt32(out var lim) ? Math.Clamp(lim, 1, 100) : 20;
        string? platform = args.TryGetProperty("platform", out var p) ? p.GetString() : null;

        Guid? workflowId = null;
        if (args.TryGetProperty("workflowId", out var wfProp) && Guid.TryParse(wfProp.GetString(), out var parsedWf))
        {
            workflowId = parsedWf;
        }

        DateTime? since = null;
        if (args.TryGetProperty("since", out var sProp) && DateTime.TryParse(sProp.GetString(), out var parsedSince))
        {
            since = parsedSince.ToUniversalTime();
        }

        bool? hasReplies = args.TryGetProperty("hasReplies", out var hrProp) && hrProp.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? hrProp.GetBoolean()
            : null;

        var query = _db.Posts
            .AsNoTracking()
            .Include(p => p.Draft)
                .ThenInclude(d => d.AutomationWorkflow)
            .Include(p => p.PlatformThread)
                .ThenInclude(pt => pt.Account)
            .Where(p => (p.UserId == userId || (p.Draft != null && p.Draft.UserId == userId)) && p.RemoteId != null);

        if (!string.IsNullOrWhiteSpace(platform))
        {
            query = query.Where(p => p.Platform == platform);
        }

        if (workflowId.HasValue)
        {
            query = query.Where(p => p.Draft != null && p.Draft.AutomationWorkflowId == workflowId.Value);
        }

        if (since.HasValue)
        {
            query = query.Where(p => p.PostedAt >= since.Value);
        }

        var posts = await query
            .OrderByDescending(p => p.PostedAt)
            .Take(limit * 2)
            .ToListAsync(ct);

        var postIds = posts.Select(p => p.Id).ToList();
        var auditCounts = await _db.WorkflowCommentAudits
            .AsNoTracking()
            .Where(a => postIds.Contains(a.PostId))
            .GroupBy(a => a.PostId)
            .Select(g => new { PostId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.PostId, g => g.Count, ct);

        var resultList = posts
            .Select(post =>
            {
                var count = auditCounts.TryGetValue(post.Id, out var c) ? c : 0;
                return new
                {
                    id = post.Id,
                    remoteId = post.RemoteId,
                    platform = post.Platform,
                    publishedText = post.PublishedText,
                    postedAt = post.PostedAt,
                    commentsCount = count,
                    auditedCommentsCount = count,
                    likesCount = 0,
                    repostsCount = 0,
                    repliesCount = count,
                    accountHandle = post.PlatformThread?.Account?.Handle ?? post.AuthorHandle,
                    workflowId = post.Draft?.AutomationWorkflowId,
                    workflowTitle = post.Draft?.AutomationWorkflow?.Title,
                    draftId = post.DraftId,
                    draftTitle = post.Draft?.Title,
                    isExternal = post.IsExternal,
                    url = post.Url,
                    authorHandle = post.AuthorHandle
                };
            });

        if (hasReplies.HasValue)
        {
            resultList = hasReplies.Value
                ? resultList.Where(p => p.commentsCount > 0)
                : resultList.Where(p => p.commentsCount == 0);
        }

        var finalResults = resultList.Take(limit).ToList();
        return TextResult(JsonSerializer.Serialize(finalResults, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleAssignReplyAgentPostAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("agentId", out var aProp) || !Guid.TryParse(aProp.GetString(), out var agentId))
        {
            return TextError("Valid 'agentId' is required.");
        }

        if (_postService == null)
        {
            return TextError("ReplyAgentPostService is not available.");
        }

        Guid? postId = null;
        if (args.TryGetProperty("postId", out var pProp) && Guid.TryParse(pProp.GetString(), out var parsedPid))
        {
            postId = parsedPid;
        }

        var url = args.TryGetProperty("url", out var uProp) ? uProp.GetString() : null;
        var remoteId = args.TryGetProperty("remoteId", out var rProp) ? rProp.GetString() : null;
        var platform = args.TryGetProperty("platform", out var plProp) ? plProp.GetString() : null;

        if (!postId.HasValue && string.IsNullOrWhiteSpace(url) && string.IsNullOrWhiteSpace(remoteId))
        {
            return TextError("Either 'url', 'remoteId', or 'postId' must be provided to assign a post.");
        }

        var req = new AssignReplyAgentPostRequest(postId, url, platform, remoteId);
        var result = await _postService.AssignPostAsync(userId, agentId, req, ct);

        if (!result.Success || result.AssignedPost == null)
        {
            return TextError(result.Error ?? "Failed to assign post to reply agent.");
        }

        return TextResult(JsonSerializer.Serialize(result.AssignedPost, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleUnassignReplyAgentPostAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("agentId", out var aProp) || !Guid.TryParse(aProp.GetString(), out var agentId))
        {
            return TextError("Valid 'agentId' is required.");
        }

        if (!args.TryGetProperty("postId", out var pProp) || !Guid.TryParse(pProp.GetString(), out var postId))
        {
            return TextError("Valid 'postId' is required.");
        }

        if (_postService == null)
        {
            return TextError("ReplyAgentPostService is not available.");
        }

        var success = await _postService.UnassignPostAsync(userId, agentId, postId, ct);
        if (!success)
        {
            return TextError($"Post assignment not found for agent '{agentId}' and post '{postId}'.");
        }

        return TextResult(JsonSerializer.Serialize(new { success = true, agentId, postId }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleListReplyAgentPostsAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (_postService == null)
        {
            return TextError("ReplyAgentPostService is not available.");
        }

        Guid? agentId = null;
        if (args.TryGetProperty("agentId", out var aProp) && Guid.TryParse(aProp.GetString(), out var parsedAid))
        {
            agentId = parsedAid;
        }

        var activeOnly = !args.TryGetProperty("activeOnly", out var aoProp) || aoProp.ValueKind != JsonValueKind.False;

        var posts = await _postService.GetAssignedPostsAsync(userId, agentId, ct);
        if (activeOnly)
        {
            posts = posts.Where(p => p.IsActive).ToList();
        }

        return TextResult(JsonSerializer.Serialize(posts, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleImportMediaFromUrlAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var dProp) || !Guid.TryParse(dProp.GetString(), out var draftId))
            return TextError("draftId must be a valid UUID.");

        if (!args.TryGetProperty("url", out var uProp) || string.IsNullOrWhiteSpace(uProp.GetString()))
            return TextError("url is required.");

        var url = uProp.GetString()!;
        var altText = args.TryGetProperty("altText", out var aProp) ? aProp.GetString() : null;

        var mediaService = ResolveMediaService();
        if (mediaService == null)
            return TextError("MediaService is not configured.");

        try
        {
            using var httpClient = ResolveHttpClient();
            var uploadResult = await mediaService.ImportMediaFromUrlAsync(userId, draftId, url, httpClient, ct, altText);
            return TextResult(JsonSerializer.Serialize(new
            {
                success = true,
                mediaId = uploadResult.Id,
                markdownTag = uploadResult.MarkdownTag,
                message = $"Media imported successfully from URL and attached to draft: {uploadResult.MarkdownTag}"
            }, JsonOptions));
        }
        catch (Exception ex)
        {
            return TextError($"Failed to import media from URL: {ex.Message}");
        }
    }

    private async Task<McpToolCallResult> HandleUploadMediaAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var dProp) || !Guid.TryParse(dProp.GetString(), out var draftId))
            return TextError("draftId must be a valid UUID.");

        if (!args.TryGetProperty("fileName", out var fProp) || string.IsNullOrWhiteSpace(fProp.GetString()))
            return TextError("fileName is required.");

        if (!args.TryGetProperty("base64Data", out var bProp) || string.IsNullOrWhiteSpace(bProp.GetString()))
            return TextError("base64Data is required.");

        var fileName = fProp.GetString()!;
        var base64Data = bProp.GetString()!;
        var mimeType = args.TryGetProperty("mimeType", out var mProp) && !string.IsNullOrWhiteSpace(mProp.GetString())
            ? mProp.GetString()!
            : "image/png";
        var altText = args.TryGetProperty("altText", out var aProp) ? aProp.GetString() : null;

        var mediaService = ResolveMediaService();
        if (mediaService == null)
            return TextError("MediaService is not configured.");

        try
        {
            if (base64Data.Contains(','))
            {
                base64Data = base64Data.Substring(base64Data.IndexOf(',') + 1);
            }

            var bytes = Convert.FromBase64String(base64Data);
            if (bytes.Length > 15 * 1024 * 1024)
            {
                return TextError("Media payload exceeds maximum size limit of 15MB.");
            }

            using var stream = new MemoryStream(bytes);
            var cmd = new UploadMediaCommand(userId, draftId, fileName, mimeType, stream, altText);
            var uploadResult = await mediaService.UploadMediaAsync(cmd, ct);

            return TextResult(JsonSerializer.Serialize(new
            {
                success = true,
                mediaId = uploadResult.Id,
                markdownTag = uploadResult.MarkdownTag,
                message = $"Media uploaded successfully and attached to draft: {uploadResult.MarkdownTag}"
            }, JsonOptions));
        }
        catch (Exception ex)
        {
            return TextError($"Failed to upload media: {ex.Message}");
        }
    }

    private async Task<McpToolCallResult> HandleListTemplatesAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        var search = args.TryGetProperty("search", out var sProp) ? sProp.GetString() : null;
        var query = _db.Templates.AsNoTracking().Where(t => t.UserId == userId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(s) || (t.Description != null && t.Description.ToLower().Contains(s)));
        }

        var templates = await query.OrderBy(t => t.Name).ToListAsync(ct);
        var items = new List<object>();
        foreach (var t in templates)
        {
            var slots = new List<object>();
            try
            {
                using var doc = JsonDocument.Parse(t.Schema);
                if (doc.RootElement.TryGetProperty("slots", out var slotsEl) && slotsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var slotEl in slotsEl.EnumerateArray())
                    {
                        var name = slotEl.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var type = slotEl.TryGetProperty("type", out var typ) ? typ.GetString() ?? "text" : "text";
                        var req = slotEl.TryGetProperty("required", out var r) && r.GetBoolean();
                        var desc = slotEl.TryGetProperty("description", out var d) ? d.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            slots.Add(new { name, type, required = req, description = desc });
                        }
                    }
                }
            }
            catch
            {
            }

            items.Add(new
            {
                id = t.Id,
                name = t.Name,
                description = t.Description,
                width = t.Width,
                height = t.Height,
                slots
            });
        }

        return TextResult(JsonSerializer.Serialize(items, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleRenderTemplateAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var dProp) || !Guid.TryParse(dProp.GetString(), out var draftId))
            return TextError("draftId must be a valid UUID.");

        if (!args.TryGetProperty("templateId", out var tProp) || string.IsNullOrWhiteSpace(tProp.GetString()))
            return TextError("templateId is required (template GUID or name).");

        var templateId = tProp.GetString()!;
        var altText = args.TryGetProperty("altText", out var aProp) ? aProp.GetString() : null;

        var slots = new Dictionary<string, string>();
        if (args.TryGetProperty("slots", out var slotsEl) && slotsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in slotsEl.EnumerateObject())
            {
                slots[prop.Name] = prop.Value.GetString() ?? prop.Value.GetRawText();
            }
        }

        var mediaService = ResolveMediaService();
        var imageEngine = ResolveImageEngine();
        if (mediaService == null || imageEngine == null)
            return TextError("Template rendering services (MediaService / ImageEngine) are not configured.");

        var renderTool = new RenderTemplateTool(_db, imageEngine, mediaService);
        var renderResult = await renderTool.ExecuteAsync(
            new RenderTemplateArgs(templateId, slots, altText),
            SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draftId));

        if (!renderResult.Success)
            return TextError(renderResult.Error ?? renderResult.Message);

        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            mediaAssetId = renderResult.MediaAssetId,
            markdownTag = renderResult.MarkdownTag,
            message = renderResult.Message
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleRenderCodeBlocksAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var dProp) || !Guid.TryParse(dProp.GetString(), out var draftId))
            return TextError("draftId must be a valid UUID.");

        var theme = args.TryGetProperty("theme", out var thProp) ? thProp.GetString() : null;
        int? blockIndex = args.TryGetProperty("blockIndex", out var biProp) && biProp.TryGetInt32(out var bi) ? bi : null;

        var codeImageService = ResolveCodeImageService();
        if (codeImageService == null)
            return TextError("CodeImageService is not configured.");

        var renderCodeTool = new RenderCodeBlocksTool(_db, codeImageService, _draftsService);
        var result = await renderCodeTool.ExecuteAsync(
            new RenderCodeBlocksArgs(theme, blockIndex),
            SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draftId));

        if (!result.Success)
            return TextError(result.Error ?? result.Message);

        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            renderedBlocks = result.RenderedBlocks,
            totalBlocks = result.TotalBlocks,
            message = result.Message
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleSearchImagesAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("query", out var qProp) || string.IsNullOrWhiteSpace(qProp.GetString()))
            return TextError("query is required.");

        var query = qProp.GetString()!;
        var searchEngine = ResolveSearchEngine();
        if (searchEngine == null)
            return TextError("Search engine is not configured.");

        var results = await searchEngine.SearchImagesAsync(query, ct);
        var formatted = results.Select(r => new
        {
            title = r.Title,
            url = r.Url,
            snippet = r.Snippet
        }).ToList();

        return TextResult(JsonSerializer.Serialize(formatted, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleUpdateMediaAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("mediaId", out var mProp) || !Guid.TryParse(mProp.GetString(), out var mediaId))
            return TextError("mediaId must be a valid UUID.");

        if (!args.TryGetProperty("altText", out var aProp))
            return TextError("altText is required.");

        var altText = aProp.GetString();
        var mediaService = ResolveMediaService();
        if (mediaService == null)
            return TextError("MediaService is not configured.");

        try
        {
            var updated = await mediaService.UpdateMediaAltTextAsync(userId, mediaId, altText, ct);
            return TextResult(JsonSerializer.Serialize(new
            {
                success = true,
                mediaId = updated.Id,
                altText = updated.AltText,
                message = "Media alt text updated successfully."
            }, JsonOptions));
        }
        catch (Exception ex)
        {
            return TextError($"Failed to update media: {ex.Message}");
        }
    }

    private async Task<McpToolCallResult> HandleDeleteMediaAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("mediaId", out var mProp) || !Guid.TryParse(mProp.GetString(), out var mediaId))
            return TextError("mediaId must be a valid UUID.");

        var mediaService = ResolveMediaService();
        if (mediaService == null)
            return TextError("MediaService is not configured.");

        try
        {
            await mediaService.DeleteMediaAsync(userId, mediaId, ct);
            return TextResult(JsonSerializer.Serialize(new
            {
                success = true,
                mediaId = mediaId,
                message = "Media deleted successfully."
            }, JsonOptions));
        }
        catch (Exception ex)
        {
            return TextError($"Failed to delete media: {ex.Message}");
        }
    }

    private async Task<McpToolCallResult> HandleSchedulePostAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var dProp) || !Guid.TryParse(dProp.GetString(), out var draftId))
            return TextError("draftId must be a valid UUID.");

        if (!args.TryGetProperty("publishAt", out var pProp) || !DateTime.TryParse(pProp.GetString(), out var publishAt))
            return TextError("publishAt must be a valid ISO 8601 UTC date-time string.");

        var publishAtUtc = publishAt.ToUniversalTime();
        if (publishAtUtc <= DateTime.UtcNow)
            return TextError("publishAt must be in the future.");

        var draft = await _db.Drafts
            .Include(d => d.Threads)
            .FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId, ct);

        if (draft == null)
            return TextError($"Draft '{draftId}' not found or access denied.");

        string? platform = args.TryGetProperty("platform", out var platProp) ? platProp.GetString() : null;
        Guid? accountId = null;
        if (args.TryGetProperty("accountId", out var accProp) && Guid.TryParse(accProp.GetString(), out var accId))
            accountId = accId;

        PlatformThread? thread = null;
        if (!string.IsNullOrWhiteSpace(platform))
        {
            thread = draft.Threads.FirstOrDefault(t => t.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase));
        }
        thread ??= draft.Threads.FirstOrDefault();

        if (thread == null)
        {
            var targetPlatform = platform ?? draft.TargetPlatform?.ToString() ?? "Bluesky";
            thread = new PlatformThread
            {
                Id = Guid.NewGuid(),
                DraftId = draft.Id,
                Platform = targetPlatform,
                Content = draft.Content,
                Stage = PlatformThreadStage.Draft,
                UpdatedAt = DateTime.UtcNow
            };
            _db.PlatformThreads.Add(thread);
            await _db.SaveChangesAsync(ct);
        }

        if (accountId.HasValue)
        {
            thread.AccountId = accountId.Value;
        }

        Guid? scheduleId = null;
        if (args.TryGetProperty("scheduleId", out var sProp) && Guid.TryParse(sProp.GetString(), out var sId))
        {
            scheduleId = sId;
        }
        else
        {
            var defaultSchedule = await _db.Schedules.FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive, ct);
            if (defaultSchedule != null)
            {
                scheduleId = defaultSchedule.Id;
            }
            else
            {
                var schedule = new Schedule
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Name = "Default Schedule",
                    Timezone = "UTC",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Schedules.Add(schedule);
                await _db.SaveChangesAsync(ct);
                scheduleId = schedule.Id;
            }
        }

        if (_queueService == null || _taskQueue == null)
            return TextError("Scheduling queue service is not available.");

        var queueItem = await _queueService.EnqueueAsync(scheduleId.Value, thread.Id, draft.Id, ct: ct);
        var delay = publishAtUtc - DateTime.UtcNow;
        var payload = new ScheduledPublishingPayload(queueItem.Id, scheduleId.Value, draft.Id, thread.Id);

        var taskId = await _taskQueue.EnqueueAsync(
            "scheduled-publishing",
            payload,
            new EnqueueTaskOptions
            {
                Delay = delay > TimeSpan.Zero ? delay : TimeSpan.Zero,
                DeduplicationKey = $"publish:{queueItem.Id}"
            },
            ct);

        queueItem.Status = ScheduleQueueItemStatus.Dispatched;
        queueItem.AssignedSlotUtc = publishAtUtc;
        queueItem.ScheduledTaskId = taskId;
        queueItem.UpdatedAt = DateTime.UtcNow;
        thread.Stage = PlatformThreadStage.Scheduled;
        await _db.SaveChangesAsync(ct);

        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            queueItemId = queueItem.Id,
            scheduleId = scheduleId.Value,
            assignedSlotUtc = publishAtUtc,
            message = $"Post scheduled for publication at {publishAtUtc:O} (QueueItem: {queueItem.Id})"
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleRemoveQueueItemAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("scheduleId", out var sProp) || !Guid.TryParse(sProp.GetString(), out var scheduleId))
            return TextError("scheduleId must be a valid UUID.");

        if (!args.TryGetProperty("queueItemId", out var qProp) || !Guid.TryParse(qProp.GetString(), out var queueItemId))
            return TextError("queueItemId must be a valid UUID.");

        var scheduleExists = await _db.Schedules.AnyAsync(s => s.Id == scheduleId && s.UserId == userId, ct);
        if (!scheduleExists)
            return TextError("Schedule not found or access denied.");

        if (_queueService == null)
            return TextError("Queue service is not available.");

        var removed = await _queueService.RemoveFromQueueAsync(scheduleId, queueItemId, ct);
        if (!removed)
            return TextError("Queue item not found in schedule.");

        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            queueItemId,
            message = "Queue item removed successfully."
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleReorderQueueAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("scheduleId", out var sProp) || !Guid.TryParse(sProp.GetString(), out var scheduleId))
            return TextError("scheduleId must be a valid UUID.");

        if (!args.TryGetProperty("itemIdsInOrder", out var idsProp) && !args.TryGetProperty("itemIds", out idsProp))
            return TextError("itemIdsInOrder must be an array of UUIDs.");

        if (idsProp.ValueKind != JsonValueKind.Array)
            return TextError("itemIdsInOrder must be an array of UUIDs.");

        var scheduleExists = await _db.Schedules.AnyAsync(s => s.Id == scheduleId && s.UserId == userId, ct);
        if (!scheduleExists)
            return TextError("Schedule not found or access denied.");

        var itemIds = new List<Guid>();
        foreach (var el in idsProp.EnumerateArray())
        {
            if (Guid.TryParse(el.GetString(), out var id))
                itemIds.Add(id);
        }

        if (_queueService == null)
            return TextError("Queue service is not available.");

        var reordered = await _queueService.ReorderQueueAsync(scheduleId, itemIds, ct);
        if (_evaluatorService != null)
            await _evaluatorService.EvaluateScheduleAsync(scheduleId, ct: ct);

        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            count = reordered.Count,
            items = reordered.Select(i => new { id = i.Id, position = i.Position, status = i.Status.ToString() })
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandlePauseQueueItemAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("scheduleId", out var sProp) || !Guid.TryParse(sProp.GetString(), out var scheduleId))
            return TextError("scheduleId must be a valid UUID.");

        if (!args.TryGetProperty("queueItemId", out var qProp) || !Guid.TryParse(qProp.GetString(), out var queueItemId))
            return TextError("queueItemId must be a valid UUID.");

        var isPaused = !args.TryGetProperty("isPaused", out var pProp) || pProp.GetBoolean();
        var scheduleExists = await _db.Schedules.AnyAsync(s => s.Id == scheduleId && s.UserId == userId, ct);
        if (!scheduleExists)
            return TextError("Schedule not found or access denied.");

        if (_queueService == null)
            return TextError("Queue service is not available.");

        var item = await _queueService.PauseQueueItemAsync(scheduleId, queueItemId, isPaused, ct);
        if (item == null)
            return TextError("Queue item not found.");

        if (!isPaused && _evaluatorService != null)
            await _evaluatorService.EvaluateScheduleAsync(scheduleId, ct: ct);

        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            queueItemId = item.Id,
            status = item.Status.ToString(),
            message = isPaused ? "Queue item paused." : "Queue item resumed."
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleRetryQueueItemAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("queueItemId", out var qProp) || !Guid.TryParse(qProp.GetString(), out var queueItemId))
            return TextError("queueItemId must be a valid UUID.");

        Guid? scheduleId = null;
        if (args.TryGetProperty("scheduleId", out var sProp) && Guid.TryParse(sProp.GetString(), out var parsedSid))
        {
            scheduleId = parsedSid;
        }

        ScheduleQueueItem? item;
        if (scheduleId.HasValue)
        {
            var scheduleExists = await _db.Schedules.AnyAsync(s => s.Id == scheduleId.Value && s.UserId == userId, ct);
            if (!scheduleExists)
                return TextError("Schedule not found or access denied.");

            item = await _db.ScheduleQueueItems.FirstOrDefaultAsync(q => q.Id == queueItemId && q.ScheduleId == scheduleId.Value, ct);
        }
        else
        {
            item = await _db.ScheduleQueueItems
                .Include(q => q.Schedule)
                .FirstOrDefaultAsync(q => q.Id == queueItemId && q.Schedule.UserId == userId, ct);
            if (item != null)
                scheduleId = item.ScheduleId;
        }

        if (item == null)
            return TextError("Queue item not found.");

        item.Status = ScheduleQueueItemStatus.Queued;
        item.LastError = null;
        item.AssignedSlotUtc = null;
        item.ScheduledTaskId = null;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (_evaluatorService != null && scheduleId.HasValue)
            await _evaluatorService.EvaluateScheduleAsync(scheduleId.Value, ct: ct);

        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            queueItemId = item.Id,
            status = item.Status.ToString(),
            message = "Queue item reset to Queued and re-evaluated."
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleUpdateScheduleAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("scheduleId", out var sProp) || !Guid.TryParse(sProp.GetString(), out var scheduleId))
            return TextError("scheduleId must be a valid UUID.");

        var schedule = await _db.Schedules.FirstOrDefaultAsync(s => s.Id == scheduleId && s.UserId == userId, ct);
        if (schedule == null)
            return TextError("Schedule not found or access denied.");

        if (args.TryGetProperty("name", out var nProp) && !string.IsNullOrWhiteSpace(nProp.GetString()))
            schedule.Name = nProp.GetString()!.Trim();

        if (args.TryGetProperty("timezone", out var tzProp) && !string.IsNullOrWhiteSpace(tzProp.GetString()))
            schedule.Timezone = tzProp.GetString()!.Trim();

        if (args.TryGetProperty("isActive", out var aProp))
            schedule.IsActive = aProp.GetBoolean();

        schedule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            scheduleId = schedule.Id,
            name = schedule.Name,
            timezone = schedule.Timezone,
            isActive = schedule.IsActive,
            message = "Schedule updated successfully."
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleDeleteScheduleAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("scheduleId", out var sProp) || !Guid.TryParse(sProp.GetString(), out var scheduleId))
            return TextError("scheduleId must be a valid UUID.");

        var schedule = await _db.Schedules
            .Include(s => s.SlotRules)
            .Include(s => s.QueueItems)
            .FirstOrDefaultAsync(s => s.Id == scheduleId && s.UserId == userId, ct);

        if (schedule == null)
            return TextError("Schedule not found or access denied.");

        _db.ScheduleSlotRules.RemoveRange(schedule.SlotRules);
        _db.ScheduleQueueItems.RemoveRange(schedule.QueueItems);
        _db.Schedules.Remove(schedule);
        await _db.SaveChangesAsync(ct);

        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            scheduleId,
            message = "Schedule and associated queue items deleted successfully."
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleDeleteFeedAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("feedId", out var fProp) || !Guid.TryParse(fProp.GetString(), out var feedId))
            return TextError("feedId must be a valid UUID.");

        var feed = await _db.FeedSubscriptions.FirstOrDefaultAsync(f => f.Id == feedId && f.UserId == userId, ct);
        if (feed == null)
            return TextError("Feed subscription not found or access denied.");

        _db.FeedSubscriptions.Remove(feed);
        await _db.SaveChangesAsync(ct);

        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            feedId,
            message = "Feed subscription deleted successfully."
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleDeleteReplyAgentAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("agentId", out var aProp) || !Guid.TryParse(aProp.GetString(), out var agentId))
            return TextError("agentId must be a valid UUID.");

        var agent = await _db.ReplyAgents.FirstOrDefaultAsync(a => a.Id == agentId && a.UserId == userId, ct);
        if (agent == null)
            return TextError("Reply agent not found or access denied.");

        _db.ReplyAgents.Remove(agent);
        await _db.SaveChangesAsync(ct);

        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            agentId,
            message = "Reply agent deleted successfully."
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleGetWorkflowAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("workflowId", out var wProp) || !Guid.TryParse(wProp.GetString(), out var workflowId))
            return TextError("workflowId must be a valid UUID.");

        var workflow = await _db.AutomationWorkflows
            .Include(w => w.Schedule)
            .Include(w => w.WorkflowSources)
                .ThenInclude(ws => ws.FeedSubscription)
            .Include(w => w.Destinations)
                .ThenInclude(d => d.Account)
            .Include(w => w.ReplyAgent)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

        if (workflow == null)
            return TextError("Workflow not found or access denied.");

        var result = new
        {
            id = workflow.Id,
            title = workflow.Title,
            instructionPrompt = workflow.InstructionPrompt,
            status = "Active",
            minRelevanceScore = workflow.MinRelevanceScore,
            schedule = workflow.Schedule != null ? new { id = workflow.Schedule.Id, name = workflow.Schedule.Name, timezone = workflow.Schedule.Timezone } : null,
            sources = workflow.WorkflowSources.Select(s => new
            {
                kind = s.Kind.ToString(),
                feedSubscriptionId = s.FeedSubscriptionId,
                feedTitle = s.FeedSubscription?.Title,
                feedUrl = s.FeedSubscription?.FeedUrl
            }),
            destinations = workflow.Destinations.Select(d => new
            {
                platform = d.Platform,
                autoPublish = d.AutoPublish,
                publishingMode = d.PublishingMode.ToString(),
                accountHandle = d.Account?.Handle,
                accountId = d.AccountId
            }),
            replyAgent = workflow.ReplyAgent != null ? new { id = workflow.ReplyAgent.Id, name = workflow.ReplyAgent.Name } : null,
            createdAt = workflow.CreatedAt,
            updatedAt = workflow.UpdatedAt
        };

        return TextResult(JsonSerializer.Serialize(result, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleGenerateVariantsAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var dProp) || !Guid.TryParse(dProp.GetString(), out var draftId))
            return TextError("draftId must be a valid UUID.");

        var platforms = new List<string>();
        if (args.TryGetProperty("platforms", out var pProp) && pProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in pProp.EnumerateArray())
            {
                if (!string.IsNullOrWhiteSpace(el.GetString()))
                    platforms.Add(el.GetString()!);
            }
        }
        if (platforms.Count == 0)
        {
            platforms.AddRange(new[] { "Bluesky", "Twitter", "MetaThreads", "LinkedIn" });
        }

        var draft = await _db.Drafts.Include(d => d.Threads).FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId, ct);
        if (draft == null)
            return TextError("Draft not found or access denied.");

        var createdVariants = new List<object>();
        foreach (var platform in platforms)
        {
            var existing = draft.Threads.FirstOrDefault(t => t.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                var newThread = new PlatformThread
                {
                    Id = Guid.NewGuid(),
                    DraftId = draft.Id,
                    Platform = platform,
                    Content = draft.Content,
                    Stage = PlatformThreadStage.Draft,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.PlatformThreads.Add(newThread);
                createdVariants.Add(new { platform, threadId = newThread.Id, stage = newThread.Stage.ToString() });
            }
            else
            {
                createdVariants.Add(new { platform, threadId = existing.Id, stage = existing.Stage.ToString() });
            }
        }

        await _db.SaveChangesAsync(ct);
        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            draftId,
            variants = createdVariants,
            message = $"Platform variants configured for draft {draftId} across {createdVariants.Count} platforms."
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleSetBlueskyReplyTargetAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var dProp) || !Guid.TryParse(dProp.GetString(), out var draftId))
            return TextError("draftId must be a valid UUID.");

        if (!args.TryGetProperty("replyParentUrl", out var uProp) || string.IsNullOrWhiteSpace(uProp.GetString()))
            return TextError("replyParentUrl is required (e.g. https://bsky.app/profile/.../post/...).");

        var replyParentUrl = uProp.GetString()!;
        var draft = await _db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId, ct);
        if (draft == null)
            return TextError("Draft not found or access denied.");

        var resolution = await _replyTargetResolver.ResolveAsync(replyParentUrl, ct);
        if (!resolution.Success)
            return TextError($"Failed to resolve Bluesky post: {resolution.Error}");

        var existingMeta = await _db.DraftBlueskyMetadata.FirstOrDefaultAsync(m => m.DraftId == draftId, ct);
        if (existingMeta == null)
        {
            existingMeta = new DraftBlueskyMetadata { DraftId = draftId };
            _db.DraftBlueskyMetadata.Add(existingMeta);
        }

        existingMeta.ReplyRootUri = resolution.ReplyRootUri!;
        existingMeta.ReplyRootCid = resolution.ReplyRootCid!;
        existingMeta.ReplyParentUri = resolution.ReplyParentUri!;
        existingMeta.ReplyParentCid = resolution.ReplyParentCid!;
        existingMeta.ReplyParentUrl = resolution.ReplyParentUrl ?? replyParentUrl;
        existingMeta.ReplyParentAuthor = resolution.ReplyParentAuthor;
        existingMeta.ReplyParentText = resolution.ReplyParentText;
        existingMeta.ReplyParentAvatarUrl = resolution.ReplyParentAvatarUrl;

        await _db.SaveChangesAsync(ct);

        return TextResult(JsonSerializer.Serialize(new
        {
            success = true,
            draftId,
            replyParentAuthor = resolution.ReplyParentAuthor,
            replyParentText = resolution.ReplyParentText,
            replyParentUrl = resolution.ReplyParentUrl ?? replyParentUrl,
            message = $"Bluesky reply target attached to draft {draftId} replying to {resolution.ReplyParentAuthor}."
        }, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleTranscribeVideoAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("url", out var uProp) || string.IsNullOrWhiteSpace(uProp.GetString()))
            return TextError("url is required (video URL to transcribe).");

        var url = uProp.GetString()!;
        var transcriber = ResolveTranscriber();
        if (transcriber == null)
            return TextError("Transcriber service is not configured.");

        try
        {
            var filename = $"mcp_transcribe_{Guid.NewGuid():N}.json";
            var result = await transcriber.ExtractAsync(url, filename, ct);
            if (!result.Success || string.IsNullOrWhiteSpace(result.TranscriptPath))
                return TextError($"Transcription failed: {result.Error ?? "Failed to extract transcript."}");

            var doc = await transcriber.ReadTranscriptAsync(result.TranscriptPath, ct);
            if (doc == null || string.IsNullOrWhiteSpace(doc.Transcript))
                return TextError("Transcript file was empty or could not be parsed.");

            return TextResult(JsonSerializer.Serialize(new
            {
                success = true,
                videoUrl = url,
                transcript = doc.Transcript,
                durationSeconds = doc.Duration ?? result.Duration,
                language = doc.Language ?? result.Language
            }, JsonOptions));
        }
        catch (Exception ex)
        {
            return TextError($"Transcription error: {ex.Message}");
        }
    }

    private async Task<McpToolCallResult> HandleWebSearchAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("query", out var qProp) || string.IsNullOrWhiteSpace(qProp.GetString()))
            return TextError("query is required.");

        var query = qProp.GetString()!;
        var searchEngine = ResolveSearchEngine();
        if (searchEngine == null)
            return TextError("Search engine is not configured.");

        try
        {
            var results = await searchEngine.SearchAsync(query, ct);
            var items = results.Select((r, i) => new
            {
                rank = i + 1,
                title = r.Title,
                url = r.Url,
                snippet = r.Snippet
            }).ToList();

            return TextResult(JsonSerializer.Serialize(new
            {
                query,
                totalResults = items.Count,
                results = items
            }, JsonOptions));
        }
        catch (Exception ex)
        {
            return TextError($"Web search failed: {ex.Message}");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private static JsonElement CreateSchema(string rawJson) => JsonDocument.Parse(rawJson).RootElement;
    private static McpToolCallResult TextResult(string text) => new(new List<McpContentPart> { new("text", Text: text) }, false);
    private static McpToolCallResult TextError(string error) => new(new List<McpContentPart> { new("text", Text: error) }, true);
}

