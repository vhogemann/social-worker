using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Background;

namespace SocialWorker.Api.Features.Sources;

public sealed class SourceTranscriptionService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly BackgroundJobQueue? _queue;

    public SourceTranscriptionService(IServiceScopeFactory? scopeFactory, BackgroundJobQueue? queue)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
    }

    public void QueueTranscriptExtraction(Guid sourceId, Guid draftId)
    {
        if (_scopeFactory == null || _queue == null)
        {
            throw new InvalidOperationException("IServiceScopeFactory and BackgroundJobQueue are required for transcript extraction jobs.");
        }

        _queue.Enqueue(new BackgroundJobQueue.Job("youtube-transcript", async ct =>
        {
            using var scope = _scopeFactory.CreateScope();
            var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var transcriber = scope.ServiceProvider.GetRequiredService<ITranscriptExtractionService>();

            var source = await scopedDb.Sources.FirstOrDefaultAsync(s => s.Id == sourceId, ct);
            if (source == null)
            {
                return;
            }

            source.TranscriptStatus = TranscriptStatus.Processing;
            await scopedDb.SaveChangesAsync(ct);

            try
            {
                var result = await transcriber.ExtractAsync(source.Reference, $"{source.Id}.json", ct);
                if (!result.Success || string.IsNullOrWhiteSpace(result.TranscriptPath))
                {
                    source.TranscriptStatus = TranscriptStatus.Failed;
                    source.Summary = result.Error;
                    await scopedDb.SaveChangesAsync(ct);
                    return;
                }

                source.TranscriptPath = result.TranscriptPath;

                var transcript = await transcriber.ReadTranscriptAsync(result.TranscriptPath, ct);
                string? text = transcript?.Transcript;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    source.Content = text;
                    var summarizer = scope.ServiceProvider.GetService<SummarizationService>();
                    if (summarizer != null)
                    {
                        try
                        {
                            source.Summary = await summarizer.SummarizeAsync(text, ct);
                        }
                        catch (Exception)
                        {
                            // Best-effort
                        }
                    }
                }

                source.TranscriptStatus = TranscriptStatus.Complete;

                var draft = await scopedDb.Drafts.FindAsync(new object[] { draftId }, ct);
                if (draft != null)
                {
                    draft.UpdatedAt = DateTime.UtcNow;
                }

                await scopedDb.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                source.TranscriptStatus = TranscriptStatus.Failed;
                source.Summary = ex.Message;
                await scopedDb.SaveChangesAsync(ct);
            }
        }));
    }
}