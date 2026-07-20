namespace SocialWorker.Api.Data;

public enum PlatformThreadStage
{
    Draft,
    Sent
}

public enum DraftStatus
{
    Editing,
    Sourcing,
    Formatting,
    Archived,
    Deleted,
    Failed
}

public enum SourceKind
{
    Url,
    File,
    YouTube
}

public enum SourceProcessingStatus
{
    Pending,
    Processing,
    Complete,
    Failed
}

public enum SocialPlatform
{
    Bluesky,
    Twitter,
    LinkedIn,
    Facebook,
    Instagram
}

public enum FeedQueueItemStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed
}