using System.Text.Json.Serialization;

namespace SocialWorker.Api.Data;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlatformThreadStage
{
    Draft,
    Scheduled,
    Sent,
    PartiallySent
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DraftStatus
{
    Editing,
    Sourcing,
    Formatting,
    Archived,
    Deleted,
    Failed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SourceKind
{
    Url,
    File,
    YouTube
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SourceProcessingStatus
{
    Pending,
    Processing,
    Complete,
    Failed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SocialPlatform
{
    Bluesky,
    Twitter,
    LinkedIn,
    Facebook,
    Instagram,
    MetaThreads
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FeedQueueItemStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkflowPublishingMode
{
    Manual,
    Automatic,
    Scheduled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SlotRecurrenceType
{
    Daily,
    Weekly,
    Monthly,
    Yearly,
    Custom
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScheduleQueueItemStatus
{
    Queued,
    Dispatched,
    Succeeded,
    Failed,
    Paused
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkflowSourceKind
{
    Feed,
    WorkflowHighlights,
    Webhook,
    EmailInbox,
    Telegram
}