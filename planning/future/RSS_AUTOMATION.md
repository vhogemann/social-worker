# Future Idea: RSS/Atom Feed Ingestion & Automation

Implement first-level automation by subscribing to external RSS/Atom feeds, periodically polling them, and converting new items into Drafts with feed-specific agent instructions.

## Proposed Concept

### 1. Feed Subscription Model
We will introduce a `FeedSubscription` entity:
- `Id` (Guid)
- `Title` (string)
- `FeedUrl` (string)
- `InstructionPrompt` (string) — Feed-specific instructions passed to the agent to draft the thread (e.g., "Summarize this tech blog post, highlight the key metrics, and keep a professional tone").
- `AutoPublish` (bool) — If true, publish immediately when ready. If false, leave the draft in the inbox for manual review.
- `LastPolledAt` (DateTime)
- `IncludeFilters` (string, nullable) — Comma-separated keywords or regex; only ingest items matching these.
- `ExcludeFilters` (string, nullable) — Comma-separated keywords or regex; ignore items matching these.

### 2. Polling Engine (Hosted Service)
A background hosted service (`FeedPollingHostedService`) will:
- Run periodically (e.g., every 30 minutes).
- Fetch feed items since `LastPolledAt`.
- For each new item:
  - Check if the item's unique identifier (`guid` or URL) has already been processed to prevent duplication.
  - If a filter is specified, match the item title/description against `IncludeFilters` and `ExcludeFilters`.
  - Create a new `Draft` and launch the automated ingestion pipeline.

---

## Key Improvements & Gaps Addressed

### A. Smart Source Detection & Full-Content Extraction
- **The Problem**: RSS/Atom feed items typically only contain a brief text summary/snippet (in `<description>` or `<summary>`) rather than the full article.
- **The Solution**: When a new feed item is ingested:
  1. Extract the primary URL (`<link>`).
  2. **Type Detection**:
     - **YouTube URLs**: If it's a YouTube link, automatically route it through the existing `YouTubeService`/`TranscriptExtractionJob` to get the full transcript.
     - **Web Articles**: For standard web links, run it through `WebScraperService` to fetch and parse the full page content/article body.
     - **Fallback**: If scraping fails, fall back to using the feed item's RSS description.
  3. Store the fully extracted content (transcript or scraped article) as the primary `Source` for the draft.

### B. Duplicate Prevention
- To prevent reprocessing the same articles, we will query the `Sources` database using the item's link URL (`Source.Reference`) before creating a new Draft. If a source with the exact URL exists, we skip it.

### C. Headless Agent Execution
- **The Problem**: Currently, thread generation is triggered interactively via chat (SSE).
- **The Solution**: Create a headless workflow execution service that can trigger an LLM run programmatically:
  1. Create a `Draft` and link the fully extracted feed source to it.
  2. Construct an initial chat message using the feed's `InstructionPrompt`.
  3. Run the LLM/Tool invocation loop in the background without requiring an active SSE connection.
  4. Once the LLM completes generation and sets the stage to `Ready`, if `AutoPublish` is true, invoke the `BlueskyPublisher` automatically.

---

## RSS/Atom Parsing Libraries (Candidates)

To avoid reinventing the wheel, we will use an existing .NET library to parse feeds:

1. **`System.ServiceModel.Syndication` (Recommended)**:
   - Microsoft's official, standard, and fully-featured syndication library.
   - Available via the `System.ServiceModel.Syndication` NuGet package for .NET 10.
   - Built-in support for RSS 2.0 and Atom 1.0, handling all namespaces, timestamps, links, and content summaries out of the box.
   - Minimal dependency footprint, officially supported.
   - Usage:
     ```csharp
     using var reader = XmlReader.Create(feedUrl);
     var feed = SyndicationFeed.Load(reader);
     foreach (var item in feed.Items)
     {
         var link = item.Links.FirstOrDefault()?.Uri?.ToString();
         var title = item.Title.Text;
     }
     ```

2. **`FeedReader`**:
   - A popular, lightweight open-source C# alternative.
   - Simplifies parsing and auto-discovery of RSS feeds from regular webpage URLs.
   - Highly active but introduces a third-party dependency.

We will use **`System.ServiceModel.Syndication`** for stability, compliance, and official Microsoft support.

---

## Database Schema
```sql
CREATE TABLE FeedSubscriptions (
    Id UUID PRIMARY KEY,
    Title VARCHAR(200) NOT NULL,
    FeedUrl VARCHAR(500) NOT NULL,
    InstructionPrompt TEXT NOT NULL,
    AutoPublish BOOLEAN NOT NULL DEFAULT FALSE,
    IncludeFilters VARCHAR(500),
    ExcludeFilters VARCHAR(500),
    LastPolledAt TIMESTAMP WITH TIME ZONE,
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);
```
