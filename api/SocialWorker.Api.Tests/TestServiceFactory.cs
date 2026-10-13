using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Sources;

namespace SocialWorker.Api.Tests;

internal static class TestServiceFactory
{
    public static SourcesService CreateSourcesService(
        AppDbContext db,
        WebScraperService? scraper = null,
        IServiceScopeFactory? scopeFactory = null,
        IBackgroundTaskQueue? queue = null,
        SummarizationService? summarizer = null)
    {
        queue ??= new TestBackgroundTaskQueue();
        scraper ??= new WebScraperService(new HttpClient(new FixedHttpMessageHandler()));

        var transcriptionService = new SourceTranscriptionService(queue);
        var youTubeSourceService = new YouTubeSourceService(db, transcriptionService);
        var urlValidator = new SourceUrlValidator();
        var urlSourceService = new UrlSourceService(db, scraper, summarizer, urlValidator, youTubeSourceService);
        var fileSourceService = new FileSourceService(db, summarizer);
        var reconciliationService = new SourceReconciliationService(db, queue);
        var sourceSearchService = new SourceSearchService(db);

        return new SourcesService(
            db,
            reconciliationService,
            sourceSearchService,
            urlSourceService,
            youTubeSourceService,
            fileSourceService);
    }

    public static DraftsService CreateDraftsService(
        AppDbContext db,
        FileStorageProvider storage,
        SourcesService sourcesService,
        IServiceScopeFactory? scopeFactory = null,
        IBackgroundTaskQueue? queue = null)
    {
        queue ??= new TestBackgroundTaskQueue();
        var draftSegmentService = new DraftSegmentService(db);
        var draftChatSummaryService = new DraftChatSummaryService(queue);
        return new DraftsService(db, storage, sourcesService, draftSegmentService, draftChatSummaryService, queue);
    }

    private sealed class FixedHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok")
            });
        }
    }
}
