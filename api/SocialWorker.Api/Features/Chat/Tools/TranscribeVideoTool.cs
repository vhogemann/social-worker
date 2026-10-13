using System;
using System.Text.Json;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Sources;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record TranscribeVideoArgs(string Url);

public sealed record TranscribeVideoResult(
    bool Success,
    string? VideoUrl,
    string? Transcript,
    double? Duration,
    string? Language,
    string? Error = null) : IChatToolResult
{
    public static implicit operator string(TranscribeVideoResult result) => result.ToDisplayText();

    public string ToDisplayText()
    {
        if (!Success)
        {
            return $"Failed to transcribe video: {Error}";
        }

        var durationStr = Duration.HasValue ? $"{Duration.Value:F0}s" : "unknown duration";
        return $"Video transcript ({Language ?? "unknown"}, {durationStr}):\n\n{Transcript}";
    }
}

public sealed class TranscribeVideoTool : ChatToolBase<TranscribeVideoArgs, TranscribeVideoResult>
{
    private readonly ITranscriptExtractionService _transcriber;

    public TranscribeVideoTool(ITranscriptExtractionService transcriber)
    {
        _transcriber = transcriber;
    }

    public override string Name => "transcribe_video";
    public override string Description => "Extract and transcribe spoken audio from a video link (e.g. YouTube video or media URL) to read what is being said in the video.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "url": {
              "type": "string",
              "description": "The URL of the video to transcribe."
            }
          },
          "required": ["url"]
        }
        """).RootElement.Clone();

    public override async Task<TranscribeVideoResult> ExecuteAsync(TranscribeVideoArgs args, ToolExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(args.Url))
        {
            return new TranscribeVideoResult(false, null, null, null, null, "No video URL provided.");
        }

        var filename = $"transcribe_tool_{Guid.NewGuid():N}.json";
        var result = await _transcriber.ExtractAsync(args.Url, filename, context.CancellationToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.TranscriptPath))
        {
            return new TranscribeVideoResult(false, args.Url, null, null, null, result.Error ?? "Failed to extract transcript.");
        }

        var doc = await _transcriber.ReadTranscriptAsync(result.TranscriptPath, context.CancellationToken);
        if (doc == null || string.IsNullOrWhiteSpace(doc.Transcript))
        {
            return new TranscribeVideoResult(false, args.Url, null, null, null, "Transcript file was empty or could not be parsed.");
        }

        return new TranscribeVideoResult(true, args.Url, doc.Transcript, doc.Duration ?? result.Duration, doc.Language ?? result.Language);
    }
}
