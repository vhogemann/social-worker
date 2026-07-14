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
    Deleted
}

public enum SourceKind
{
    Url,
    File,
    YouTube
}

public enum TranscriptStatus
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