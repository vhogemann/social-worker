using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Mcp.Models;
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
        IReplyPlatformAdapterResolver? replyAdapterResolver = null)
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
    }

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
                    "platform": { "type": "string", "description": "Optional target platform (e.g. 'Bluesky')." }
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
                    "platform": { "type": "string", "description": "Target platform (defaults to 'Bluesky')." }
                },
                "required": ["draftId"]
            }
            """)),
            new("publish_thread", "Publish a draft to a live social media platform (e.g. Bluesky) with server-enforced validation.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "draftId": { "type": "string", "description": "The UUID of the draft to publish." },
                    "platform": { "type": "string", "description": "The target platform (e.g. 'Bluesky')." }
                },
                "required": ["draftId", "platform"]
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
                    "subscriptionId": { "type": "string", "description": "Optional UUID of a specific feed subscription." }
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
            new("enqueue_draft", "Enqueue an existing draft into a schedule's publishing queue.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "scheduleId": { "type": "string", "description": "The UUID of the schedule." },
                    "draftId": { "type": "string", "description": "The UUID of the draft to enqueue." },
                    "targetPosition": { "type": "integer", "description": "Optional specific 1-based queue position. Defaults to appending to end of queue." }
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
            new("discover_feed", "Discover the RSS/Atom feed URL, channel title, and website URL from any website, YouTube channel/playlist, or Instagram profile (e.g. 'https://www.instagram.com/flaviobolsonaro').", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "url": { "type": "string", "description": "The URL of the website, YouTube channel/playlist, or Instagram profile." }
                },
                "required": ["url"]
            }
            """)),
            new("add_feed", "Subscribe to a new RSS, YouTube, or Instagram feed (e.g. 'instagram://flaviobolsonaro').", CreateSchema("""
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
                    "scheduleId": { "type": "string", "description": "Optional schedule UUID for workflow-level scheduling." }
                },
                "required": ["title"]
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
            new("get_reply_agent", "Get the autonomous reply agent configuration, settings, and activity statistics for an automation workflow.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "workflowId": { "type": "string", "description": "The unique UUID of the automation workflow." }
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
                    "checkIntervalMinutes": { "type": "integer", "description": "Polling interval in minutes (default: 5)." }
                },
                "required": ["workflowId"]
            }
            """)),
            new("trigger_reply_check", "Trigger the reply agent for a workflow to check for comments and evaluate replies. Can run synchronously to immediately return evaluation results or asynchronously in background.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "workflowId": { "type": "string", "description": "The unique UUID of the automation workflow." },
                    "postId": { "type": "string", "description": "Optional UUID of a specific post to check immediately." },
                    "runSynchronously": { "type": "boolean", "description": "If true, executes immediately and returns evaluated comments and replies. If false, queues background check tasks (default: false)." }
                },
                "required": ["workflowId"]
            }
            """)),
            new("list_comment_audits", "List audited comments seen by a workflow's reply agent, including LLM ranking scores, decisions, reasoning, and generated replies.", CreateSchema("""
            {
                "type": "object",
                "properties": {
                    "workflowId": { "type": "string", "description": "The unique UUID of the automation workflow." },
                    "decision": { "type": "string", "description": "Optional filter by decision: 'Replied', 'SkippedBelowThreshold', 'SkippedStopCriteria', 'PendingApproval', 'Failed'." },
                    "limit": { "type": "integer", "description": "Maximum number of audit records to return (default: 20)." },
                    "offset": { "type": "integer", "description": "Number of audit records to skip for pagination (default: 0)." }
                },
                "required": ["workflowId"]
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
                "enqueue_draft" => await HandleEnqueueDraftAsync(userId, args, ct),
                "get_schedule_queue" => await HandleGetScheduleQueueAsync(userId, args, ct),
                "get_calendar_events" => await HandleGetCalendarEventsAsync(userId, args, ct),
                "list_accounts" => await HandleListAccountsAsync(userId, args, ct),
                "discover_feed" => await HandleDiscoverFeedAsync(args, ct),
                "add_feed" => await HandleAddFeedAsync(userId, args, ct),
                "update_feed" => await HandleUpdateFeedAsync(userId, args, ct),
                "create_workflow" => await HandleCreateWorkflowAsync(userId, args, ct),
                "delete_workflow" => await HandleDeleteWorkflowAsync(userId, args, ct),
                "get_reply_agent" => await HandleGetReplyAgentAsync(userId, args, ct),
                "configure_reply_agent" => await HandleConfigureReplyAgentAsync(userId, args, ct),
                "trigger_reply_check" => await HandleTriggerReplyCheckAsync(userId, args, ct),
                "list_comment_audits" => await HandleListCommentAuditsAsync(userId, args, ct),
                "reply_to_comment" => await HandleReplyToCommentAsync(userId, args, ct),
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
        return TextResult(JsonSerializer.Serialize(draft, JsonOptions));
    }


    private async Task<McpToolCallResult> HandleCreateDraftAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        var title = args.GetProperty("title").GetString() ?? "New Draft";
        var content = args.TryGetProperty("content", out var c) ? c.GetString() : null;
        var platform = args.TryGetProperty("platform", out var p) ? p.GetString() : null;

        var result = await _draftsService.CreateDraftAsync(userId, title, content, platform, null, ct);
        return TextResult($"Draft created successfully with ID: {result.Id}");
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

    private async Task<McpToolCallResult> HandleValidateDraftAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var draftId))
        {
            return TextError("draftId must be a valid UUID.");
        }

        var platform = args.TryGetProperty("platform", out var p) ? p.GetString() : "Bluesky";
        var validatorTool = new ValidateDraftTool(_db, _validatorResolver);
        var validation = await validatorTool.ExecuteAsync(new ValidateDraftArgs(null, platform), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draftId));
        return TextResult(validation.ToDisplayText());
    }

    private async Task<McpToolCallResult> HandlePublishThreadAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("draftId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var draftId))
        {
            return TextError("draftId must be a valid UUID.");
        }

        var platform = args.GetProperty("platform").GetString() ?? "Bluesky";
        var publishTool = new PublishPlatformTool(_db, _publisherResolver, _blueskyValidator, _replyTargetResolver);
        var result = await publishTool.ExecuteAsync(new PublishPlatformArgs(platform), SocialWorker.Api.Features.Chat.Models.ToolExecutionContext.Create(userId, draftId));

        if (!result.Success)
        {
            return TextError(result.Error ?? result.Message);
        }

        return TextResult(result.Message);
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

        var reference = args.GetProperty("reference").GetString() ?? "";
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

        var dtos = workflows.Select(w => new AutomationWorkflowDto(w)).ToList();
        return TextResult(JsonSerializer.Serialize(dtos, JsonOptions));
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
        if (args.TryGetProperty("subscriptionId", out var idProp) && Guid.TryParse(idProp.GetString(), out var subId))
        {
            await _feedPollingService.PollSubscriptionAsync(subId, ct);
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
                platformThreadId = existingThread.Id;
            }
            else
            {
                var newThread = new PlatformThread
                {
                    Id = Guid.NewGuid(),
                    DraftId = draftId,
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

        var item = await _queueService.EnqueueAsync(scheduleId, platformThreadId, draftId, targetPosition, ct);

        if (_evaluatorService != null)
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

        var workflow = AutomationWorkflow.Create(userId, title, instructionPrompt, scheduleId);
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

    private async Task<McpToolCallResult> HandleGetReplyAgentAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("workflowId", out var wfProp) || !Guid.TryParse(wfProp.GetString(), out var workflowId))
        {
            return TextError("workflowId is required and must be a valid UUID.");
        }

        var workflow = await _db.AutomationWorkflows
            .Include(w => w.ReplyAgent)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

        if (workflow == null)
        {
            return TextError("Workflow not found.");
        }

        if (workflow.ReplyAgent == null)
        {
            return TextResult(JsonSerializer.Serialize(new { configured = false, workflowId }, JsonOptions));
        }

        var agent = workflow.ReplyAgent;
        var totalAudits = await _db.WorkflowCommentAudits.CountAsync(a => a.WorkflowReplyAgentId == agent.Id, ct);
        var repliedCount = await _db.WorkflowCommentAudits.CountAsync(a => a.WorkflowReplyAgentId == agent.Id && a.RankingDecision == "Replied", ct);
        var pendingCount = await _db.WorkflowCommentAudits.CountAsync(a => a.WorkflowReplyAgentId == agent.Id && a.RankingDecision == "PendingApproval", ct);

        var response = new
        {
            configured = true,
            agent = new WorkflowReplyAgentDto(agent),
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
        if (!args.TryGetProperty("workflowId", out var wfProp) || !Guid.TryParse(wfProp.GetString(), out var workflowId))
        {
            return TextError("workflowId is required and must be a valid UUID.");
        }

        var workflow = await _db.AutomationWorkflows
            .Include(w => w.ReplyAgent)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

        if (workflow == null)
        {
            return TextError("Workflow not found.");
        }

        var personaPrompt = args.TryGetProperty("personaPrompt", out var pProp) ? pProp.GetString() : null;
        var isEnabled = !args.TryGetProperty("isEnabled", out var enProp) || enProp.ValueKind != JsonValueKind.False;
        var maxCommentAgeHours = args.TryGetProperty("maxCommentAgeHours", out var ahProp) && ahProp.TryGetInt32(out var ah) ? ah : 24;
        var maxPostAgeDays = args.TryGetProperty("maxPostAgeDays", out var pdProp) && pdProp.TryGetInt32(out var pd) ? pd : 7;
        var minRankingScore = args.TryGetProperty("minRankingScore", out var msProp) && msProp.TryGetInt32(out var ms) ? ms : 7;
        var maxRepliesPerBranch = args.TryGetProperty("maxRepliesPerCommentThread", out var mrProp) && mrProp.TryGetInt32(out var mr) ? mr : 3;
        var maxRepliesPerUser = args.TryGetProperty("maxRepliesPerUser", out var muProp) && muProp.TryGetInt32(out var mu) ? mu : 2;
        var maxThreadDepth = args.TryGetProperty("maxThreadDepth", out var tdProp) && tdProp.TryGetInt32(out var td) ? td : 3;
        var autoPublishReplies = !args.TryGetProperty("autoPublishReplies", out var apProp) || apProp.ValueKind != JsonValueKind.False;
        var checkIntervalMinutes = args.TryGetProperty("checkIntervalMinutes", out var ciProp) && ciProp.TryGetInt32(out var ci) ? ci : 5;

        var agent = workflow.ReplyAgent;
        if (agent == null)
        {
            agent = WorkflowReplyAgent.Create(
                workflowId,
                string.IsNullOrWhiteSpace(personaPrompt) ? "You are a helpful and polite social media assistant." : personaPrompt.Trim(),
                maxCommentAgeHours,
                maxPostAgeDays,
                minRankingScore,
                maxRepliesPerBranch,
                maxRepliesPerUser,
                maxThreadDepth,
                autoPublishReplies,
                checkIntervalMinutes);
            agent.IsEnabled = isEnabled;
            _db.WorkflowReplyAgents.Add(agent);
        }
        else
        {
            agent.IsEnabled = isEnabled;
            if (!string.IsNullOrWhiteSpace(personaPrompt))
            {
                agent.PersonaPrompt = personaPrompt.Trim();
            }
            agent.MaxCommentAgeHours = Math.Max(1, maxCommentAgeHours);
            agent.MaxPostAgeDays = Math.Max(1, maxPostAgeDays);
            agent.MinRankingScore = Math.Clamp(minRankingScore, 1, 10);
            agent.MaxRepliesPerCommentThread = Math.Max(1, maxRepliesPerBranch);
            agent.MaxRepliesPerUser = Math.Max(1, maxRepliesPerUser);
            agent.MaxThreadDepth = Math.Max(1, maxThreadDepth);
            agent.AutoPublishReplies = autoPublishReplies;
            agent.CheckIntervalMinutes = Math.Max(1, checkIntervalMinutes);
            agent.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return TextResult(JsonSerializer.Serialize(new WorkflowReplyAgentDto(agent), JsonOptions));
    }

    private async Task<McpToolCallResult> HandleTriggerReplyCheckAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("workflowId", out var wfProp) || !Guid.TryParse(wfProp.GetString(), out var workflowId))
        {
            return TextError("workflowId is required and must be a valid UUID.");
        }

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

        var agent = workflow.ReplyAgent;
        var runSynchronously = args.TryGetProperty("runSynchronously", out var syncProp) && syncProp.ValueKind == JsonValueKind.True;

        Guid? specificPostId = null;
        if (args.TryGetProperty("postId", out var pProp) && Guid.TryParse(pProp.GetString(), out var parsedPid))
        {
            specificPostId = parsedPid;
        }

        var cutoff = DateTime.UtcNow.Subtract(TimeSpan.FromDays(agent.MaxPostAgeDays));
        var postsQuery = _db.Posts
            .Include(p => p.Draft)
            .Where(p => p.Draft.AutomationWorkflowId == workflow.Id && p.RemoteId != null && p.PostedAt >= cutoff);

        if (specificPostId.HasValue)
        {
            postsQuery = postsQuery.Where(p => p.Id == specificPostId.Value);
        }

        var posts = await postsQuery.ToListAsync(ct);
        if (posts.Count == 0)
        {
            return TextResult($"No eligible published posts found for workflow '{workflow.Title}'.");
        }

        if (runSynchronously && _replyCoordinator != null)
        {
            var startTime = DateTime.UtcNow;
            foreach (var post in posts)
            {
                await _replyCoordinator.ProcessPostCommentsAsync(agent.Id, post.Id, ct);
            }

            var recentAudits = await _db.WorkflowCommentAudits
                .Where(a => a.WorkflowReplyAgentId == agent.Id && a.CreatedAt >= startTime.AddSeconds(-2))
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

        return TextResult($"Enqueued reply check for {enqueuedCount} post(s) for workflow '{workflow.Title}'.");
    }

    private async Task<McpToolCallResult> HandleListCommentAuditsAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("workflowId", out var wfProp) || !Guid.TryParse(wfProp.GetString(), out var workflowId))
        {
            return TextError("workflowId is required and must be a valid UUID.");
        }

        var workflow = await _db.AutomationWorkflows
            .Include(w => w.ReplyAgent)
            .FirstOrDefaultAsync(w => w.Id == workflowId && w.UserId == userId, ct);

        if (workflow == null)
        {
            return TextError("Workflow not found.");
        }

        if (workflow.ReplyAgent == null)
        {
            return TextResult("[]");
        }

        var query = _db.WorkflowCommentAudits
            .Where(a => a.WorkflowReplyAgentId == workflow.ReplyAgent.Id);

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
                .Include(a => a.WorkflowReplyAgent)
                    .ThenInclude(ra => ra.AutomationWorkflow)
                .Include(a => a.Post)
                    .ThenInclude(p => p.PlatformThread)
                        .ThenInclude(pt => pt.Account)
                .FirstOrDefaultAsync(a => a.Id == auditId, ct);

            if (audit == null)
            {
                return TextError($"Comment audit {auditId} not found.");
            }

            if (audit.WorkflowReplyAgent?.AutomationWorkflow?.UserId != userId)
            {
                return TextError("Unauthorized: Comment audit does not belong to your workflows.");
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

