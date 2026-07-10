using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Infrastructure.Search;

public record SearchResult(string Title, string Url, string Snippet);

public interface ISearchEngine
{
    Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct);
    Task<List<SearchResult>> SearchImagesAsync(string query, CancellationToken ct) => Task.FromResult(new List<SearchResult>());
}
