using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Mcp.Models;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Features.Scheduling.Models;
using SocialWorker.Api.Features.Scheduling.Services;
using SocialWorker.Api.Features.Sources;

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
        ICalendarProjectionService? calendarService = null)
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
    }

    public List<McpToolDefinition> ListTools()
    {
        return new List<McpToolDefinition>
        {
            new("list_drafts", "List user social media drafts with status and title.", CreateSchema("""
            {
                "type": "object",
                "properties": {
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
        var drafts = await _draftsService.GetDraftsForUserAsync(userId, ct);
        var selected = drafts.Take(limit).Select(d => new
        {
            d.Id,
            d.Title,
            d.Status,
            d.TargetPlatform,
            d.UpdatedAt
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
            .Select(w => new { w.Id, w.Title, w.InstructionPrompt, w.CreatedAt })
            .ToListAsync(ct);

        return TextResult(JsonSerializer.Serialize(workflows, JsonOptions));
    }

    private async Task<McpToolCallResult> HandleTriggerWorkflowAsync(Guid userId, JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("workflowId", out var idProp) || !Guid.TryParse(idProp.GetString(), out var workflowId))
        {
            return TextError("workflowId must be a valid UUID.");
        }

        var sources = await _db.AutomationWorkflowSources
            .Where(s => s.AutomationWorkflowId == workflowId)
            .Select(s => s.FeedSubscriptionId)
            .ToListAsync(ct);

        foreach (var subId in sources)
        {
            await _feedPollingService.PollSubscriptionAsync(subId, ct);
        }

        return TextResult($"Triggered polling for workflow {workflowId} across {sources.Count} linked feed(s).");
    }

    private async Task<McpToolCallResult> HandleListFeedsAsync(Guid userId, CancellationToken ct)
    {
        var feeds = await _db.FeedSubscriptions
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => new { f.Id, f.Title, f.FeedUrl, f.WebsiteUrl, f.LastPolledAt })
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static JsonElement CreateSchema(string rawJson) => JsonDocument.Parse(rawJson).RootElement;
    private static McpToolCallResult TextResult(string text) => new(new List<McpContentPart> { new("text", Text: text) }, false);
    private static McpToolCallResult TextError(string error) => new(new List<McpContentPart> { new("text", Text: error) }, true);
}

