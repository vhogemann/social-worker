namespace SocialWorker.Api.Data;

public enum PlatformThreadStage
{
    Draft,
    Ready,
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