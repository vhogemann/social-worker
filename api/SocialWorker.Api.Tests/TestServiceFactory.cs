using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Infrastructure.Background;

namespace SocialWorker.Api.Tests;

internal static class TestServiceFactory
{
    public static SourcesService CreateSourcesService(
        AppDbContext db,
        WebScraperService? scraper = null,
        IServiceScopeFactory? scopeFactory = null,
        BackgroundJobQueue? queue = null,
        SummarizationService? summarizer = null)
    {
        scopeFactory ??= new EmptyScopeFactory();
        queue ??= new BackgroundJobQueue();
        scraper ??= new WebScraperService(new HttpClient(new FixedHttpMessageHandler()));

        var transcriptionService = new SourceTranscriptionService(scopeFactory, queue);
        var youTubeSourceService = new YouTubeSourceService(db, transcriptionService);
        var urlValidator = new SourceUrlValidator();
        var urlSourceService = new UrlSourceService(db, scraper, summarizer, urlValidator, youTubeSourceService);
        var fileSourceService = new FileSourceService(db, summarizer);
        var reconciliationService = new SourceReconciliationService(db, scopeFactory, queue);
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
        BackgroundJobQueue? queue = null)
    {
        scopeFactory ??= new EmptyScopeFactory();
        queue ??= new BackgroundJobQueue();
        var draftSegmentService = new DraftSegmentService(db);
        var draftChatSummaryService = new DraftChatSummaryService(scopeFactory, queue);
        return new DraftsService(db, storage, sourcesService, draftSegmentService, draftChatSummaryService);
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

    private sealed class EmptyScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            var provider = new ServiceCollection().BuildServiceProvider();
            return new EmptyScope(provider);
        }

        private sealed class EmptyScope : IServiceScope
        {
            public EmptyScope(IServiceProvider provider)
            {
                ServiceProvider = provider;
            }

            public IServiceProvider ServiceProvider { get; }

            public void Dispose()
            {
            }
        }
    }
}
