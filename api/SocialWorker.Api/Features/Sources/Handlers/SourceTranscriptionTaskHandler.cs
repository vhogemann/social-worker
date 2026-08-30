using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;

namespace SocialWorker.Api.Features.Sources.Handlers;

public sealed record SourceTranscriptionPayload(Guid SourceId, Guid DraftId);

public sealed class SourceTranscriptionTaskHandler : BackgroundTaskHandlerBase<SourceTranscriptionPayload>
{
    private readonly AppDbContext _db;
    private readonly ITranscriptExtractionService _transcriber;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SourceTranscriptionTaskHandler> _logger;

    public override string TaskType => "source-transcription";
    public override TimeSpan DefaultLeaseTimeout => TimeSpan.FromMinutes(15);
    public override int DefaultMaxAttempts => 3;

    public SourceTranscriptionTaskHandler(
        AppDbContext db,
        ITranscriptExtractionService transcriber,
        IServiceProvider serviceProvider,
        ILogger<SourceTranscriptionTaskHandler> logger)
    {
        _db = db;
        _transcriber = transcriber;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override async Task<TaskResult> ExecuteAsync(
        SourceTranscriptionPayload payload,
        ITaskExecutionContext context,
        CancellationToken ct)
    {
        var source = await _db.Sources.FirstOrDefaultAsync(s => s.Id == payload.SourceId, ct);
        if (source == null)
        {
            return TaskResult.Fatal($"Source {payload.SourceId} not found");
        }

        source.ProcessingStatus = SourceProcessingStatus.Processing;
        await _db.SaveChangesAsync(ct);

        try
        {
            var result = await _transcriber.ExtractAsync(source.Reference, $"{source.Id}.json", ct);
            if (!result.Success || string.IsNullOrWhiteSpace(result.TranscriptPath))
            {
                source.ProcessingStatus = SourceProcessingStatus.Failed;
                source.Summary = result.Error;
                if (context.AttemptCount >= context.MaxAttempts)
                {
                    var draft = await _db.Drafts.FindAsync(new object[] { payload.DraftId }, ct);
                    if (draft != null && draft.AutomationWorkflowId.HasValue)
                    {
                        draft.Status = DraftStatus.Failed;
                        draft.Title = $"[Failed Ingestion] {draft.Title}";
                    }
                }
                await _db.SaveChangesAsync(ct);
                return TaskResult.Retry(TimeSpan.FromSeconds(30), result.Error ?? "Transcription extraction returned failure");
            }

            source.TranscriptPath = result.TranscriptPath;

            var transcript = await _transcriber.ReadTranscriptAsync(result.TranscriptPath, ct);
            string? text = transcript?.Transcript;
            if (!string.IsNullOrWhiteSpace(text))
            {
                source.Content = text;
                var summarizer = _serviceProvider.GetService<SummarizationService>();
                if (summarizer != null)
                {
                    try
                    {
                        source.Summary = await summarizer.SummarizeAsync(text, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Summarization failed for source {SourceId}", source.Id);
                    }
                }
            }

            source.ProcessingStatus = SourceProcessingStatus.Complete;

            var targetDraft = await _db.Drafts.FindAsync(new object[] { payload.DraftId }, ct);
            if (targetDraft != null)
            {
                targetDraft.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);

                if (targetDraft.AutomationWorkflowId.HasValue && (targetDraft.Status == DraftStatus.Editing || string.IsNullOrWhiteSpace(targetDraft.Content)))
                {
                    var orchestrator = _serviceProvider.GetService<SocialWorker.Api.Features.Feeds.FeedOrchestrationService>();
                    if (orchestrator != null)
                    {
                        try
                        {
                            await orchestrator.ContinueWorkflowDraftAfterTranscriptionAsync(targetDraft.Id, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Downstream workflow orchestration failed for draft {DraftId} after transcription", targetDraft.Id);
                        }
                    }
                }
            }
            else
            {
                await _db.SaveChangesAsync(ct);
            }

            return TaskResult.Success();
        }
        catch (Exception ex)
        {
            source.ProcessingStatus = SourceProcessingStatus.Failed;
            source.Summary = ex.Message;
            if (context.AttemptCount >= context.MaxAttempts)
            {
                var draft = await _db.Drafts.FindAsync(new object[] { payload.DraftId }, ct);
                if (draft != null && draft.AutomationWorkflowId.HasValue)
                {
                    draft.Status = DraftStatus.Failed;
                    draft.Title = $"[Failed Ingestion] {draft.Title}";
                }
            }
            await _db.SaveChangesAsync(ct);
            return TaskResult.Retry(TimeSpan.FromSeconds(30), ex.Message);
        }
    }
}
