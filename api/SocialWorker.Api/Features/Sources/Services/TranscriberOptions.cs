namespace SocialWorker.Api.Features.Sources;

public sealed class TranscriberOptions
{
    public const string SectionName = "Transcriber";

    public string BaseUrl { get; set; } = "http://localhost:8102";
    public int TimeoutSeconds { get; set; } = 1800;
    public string TranscriptsPath { get; set; } = "/app/transcripts";
}