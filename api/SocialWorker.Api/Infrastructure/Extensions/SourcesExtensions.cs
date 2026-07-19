using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddSources(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<TranscriberOptions>(builder.Configuration.GetSection(TranscriberOptions.SectionName));
        builder.Services.AddHttpClient<WebScraperService>();
        builder.Services.AddHttpClient<ITranscriptExtractionService, TranscriptExtractionService>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TranscriberOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        builder.Services.AddHttpClient<SummarizationService>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TranscriberOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        builder.Services.AddScoped<SourceExtractor>();
        builder.Services.AddScoped<ISourceUrlValidator, SourceUrlValidator>();
        builder.Services.AddScoped<SourceReconciliationService>();
        builder.Services.AddScoped<SourceTranscriptionService>();
        builder.Services.AddScoped<IYouTubeSourceService, YouTubeSourceService>();
        builder.Services.AddScoped<IUrlSourceService, UrlSourceService>();
        builder.Services.AddScoped<IFileSourceService, FileSourceService>();
        builder.Services.AddScoped<SourceSearchService>();
        builder.Services.AddScoped<SourcesService>();
    }
}
