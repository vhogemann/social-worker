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
    private readonly BackgroundJobQueue _queue;

    public SourceTranscriptionService(BackgroundJobQueue queue)
    {
        _queue = queue;
    }

    public void QueueTranscriptExtraction(Guid sourceId, Guid draftId)
    {
        _queue.EnqueueScoped("youtube-transcript", async (sp, ct) =>
        {
            var scopedDb = sp.GetRequiredService<AppDbContext>();
            var transcriber = sp.GetRequiredService<ITranscriptExtractionService>();

            var source = await scopedDb.Sources.FirstOrDefaultAsync(s => s.Id == sourceId, ct);
            if (source == null)
            {
                return;
            }

            source.ProcessingStatus = SourceProcessingStatus.Processing;
            await scopedDb.SaveChangesAsync(ct);

            try
            {
                var result = await transcriber.ExtractAsync(source.Reference, $"{source.Id}.json", ct);
                if (!result.Success || string.IsNullOrWhiteSpace(result.TranscriptPath))
                {
                    source.ProcessingStatus = SourceProcessingStatus.Failed;
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
                    var summarizer = sp.GetService<SummarizationService>();
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

                source.ProcessingStatus = SourceProcessingStatus.Complete;

                var draft = await scopedDb.Drafts.FindAsync(new object[] { draftId }, ct);
                if (draft != null)
                {
                    draft.UpdatedAt = DateTime.UtcNow;
                }

                await scopedDb.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                source.ProcessingStatus = SourceProcessingStatus.Failed;
                source.Summary = ex.Message;
                await scopedDb.SaveChangesAsync(ct);
            }
        });
    }
}