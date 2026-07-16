using SocialWorker.Api.Infrastructure.Search;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddSearch(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection(SearchOptions.SectionName));
        builder.Services.AddHttpClient<BraveSearchEngine>();
        builder.Services.AddHttpClient<SearXngSearchEngine>();
        builder.Services.AddScoped<ISearchEngine>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SearchOptions>>().Value;
            if (string.Equals(opts.Provider, "Brave", StringComparison.OrdinalIgnoreCase))
            {
                return sp.GetRequiredService<BraveSearchEngine>();
            }
            return sp.GetRequiredService<SearXngSearchEngine>();
        });
    }
}
