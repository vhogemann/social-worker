# Web Search, Sources, and Preview Integration Plan

We want to add search and source-management tools to the agent so that it can retrieve current information from the web and attach websites, documents, images, and videos as draft sources. We will also add a WebUI preview functionality for all source types.

## Architecture

We will implement:
1. A configurable search infrastructure supporting **Brave Search API** and local **SearXNG**.
2. An `add_source` tool that allows the agent to explicitly add URL, YouTube, or reference document sources, automatically scraping/extracting content when applicable.
3. A source preview API and a frontend Modal to preview source contents (including responsive YouTube player embeds).

### Search Options Config
We will add a new config section to the API settings:
```json
"Search": {
  "Provider": "Brave", // "Brave" or "SearXng"
  "BraveApiKey": "",
  "SearXngBaseUrl": "http://searxng:8080"
}
```

### Abstractions & Implementations
- `ISearchEngine`:
  ```csharp
  public interface ISearchEngine
  {
      Task<List<SearchResult>> SearchAsync(string query, CancellationToken ct);
  }
  ```
- `SearchResult`:
  ```csharp
  public record SearchResult(string Title, string Url, string Snippet);
  ```
- `BraveSearchEngine`: Calls `https://api.search.brave.com/res/v1/web/search?q={query}`.
- `SearXngSearchEngine`: Calls `{SearXngBaseUrl}/search?q={query}&format=json`.

### Chat Tools
1. **`WebSearchTool` (`web_search`)**:
   - Description: "Search the web for current information, facts, news, or articles."
   - Parameters: `{ "query": "string" }`

2. **`AddSourceTool` (`add_source`)**:
   - Description: "Add a web URL, YouTube video link, or document reference as a source for this draft."
   - Parameters:
     - `kind`: `"Url"` | `"YouTube"` | `"File"`
     - `reference`: The URL, video link, or file name.
     - `title`: (Optional) Custom title.
     - `content`: (Optional) Text content.
   - Behavior: If `kind` is `Url` or `YouTube` and `content` is empty, the server will scrape and cache the webpage's readable text content.

### Source Preview API
- **Endpoint**: `GET /api/drafts/{draftId}/sources/{sourceId}`
  - Returns `SourceDetailDto` which includes the full `Content` property of the source.
- **Service Method**: `GetSourceDetailAsync` in `SourcesService`.

### Frontend Preview Modal
- Render a preview button (eye icon) next to each source in `SourcesPanel.tsx`.
- On click, open a Modal:
  - If **YouTube**: Parse the reference URL for video ID (e.g. `dQw4w9WgXcQ`), and render an `<iframe>` player (`https://www.youtube.com/embed/{id}`) for direct in-app watching.
  - If **Url** or **File**: Fetch details using `fetchSourceDetail`, then render the text content in a scrollable, read-only pane (preserving formatting).
