# RSS/Atom Feed Ingestion & Automation

Implement first-level automation by subscribing to external RSS/Atom feeds, periodically polling them, and converting new items into Drafts with feed-specific agent instructions.

## Decisions & Design Specifications

### 1. Feed Subscription Model
We will introduce a `FeedSubscription` entity linked to `AppUser`:
- `Id` (Guid)
- `UserId` (Guid) — Owner of the subscription.
- `Title` (string) — User-defined title for the subscription.
- `FeedUrl` (string) — The resolved RSS/Atom feed URL.
- `WebsiteUrl` (string) — The original website URL entered by the user (if any) prior to feed auto-discovery.
- `InstructionPrompt` (string) — Feed-specific instructions passed to the agent to draft the thread (e.g., "Summarize this tech blog post, highlight the key metrics, and keep a professional tone").
- `AutoPublish` (bool) — If true, publish immediately when ready. If false, leave the draft in the inbox for manual review.
- `LastPolledAt` (DateTime, nullable)
- `IncludeFilters` (string, nullable) — Comma-separated keywords or regex; only ingest items matching these.
- `ExcludeFilters` (string, nullable) — Comma-separated keywords or regex; ignore items matching these.
- `CreatedAt` (DateTime)

### 2. Polling Engine (Hosted Service)
A background hosted service (`FeedPollingHostedService`) will:
- Run periodically (every 30 minutes, or configured via appsettings/timer).
- Fetch feed items since `LastPolledAt` or from a window of the last 24 hours if `LastPolledAt` is null.
- For each new item:
  - Check if the item's unique identifier (`guid` or URL) has already been processed to prevent duplication.
  - If filters are specified, match the item title/description against `IncludeFilters` and `ExcludeFilters`.
  - Create a new `Draft` in the `Sourcing` stage and launch the automated ingestion pipeline.

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
  1. Create a `Draft` in the `Sourcing` stage and link the initial feed source.
  2. Start the background ingestion pipeline (scraping / YouTube transcription).
  3. **Ingestion Gate**: Wait for the ingestion/transcription background job to finish. The draft generation loop is only triggered once the `Source.TranscriptStatus` or ingestion state reaches `Complete`. If ingestion fails, mark the draft as `Failed` and abort generation.
  4. Construct the initial chat message using the feed's `InstructionPrompt` referencing the fully ingested source.
  5. Run the LLM/Tool invocation loop in the background without requiring an active SSE connection.
  6. Once the LLM completes generation and sets the stage to `Ready`, if `AutoPublish` is true, invoke the `BlueskyPublisher` automatically.

---

## RSS/Atom Parsing Library

We will use **`FeedReader`**:
- A popular, lightweight open-source C# alternative.
- Available via NuGet package `CodeHollow.FeedReader`.
- Simplifies parsing and supports auto-discovery of RSS feeds from regular webpage URLs out of the box.
- API auto-discovery will run asynchronously when visiting a dashboard or upon background parsing rather than blocking save requests.

---

## Feed Auto-Discovery & YouTube Channel Integration

### 1. General RSS Auto-Discovery
To make subscribing user-friendly, when a user enters a general URL (like `https://someblog.com` or `https://substack.com/profile`), the application will:
- Check if it's a direct XML feed. If not, use `FeedReader.GetFeedUrlsFromPageAsync(url)` or parse HTML to find feed links.
- Store both the user-entered URL (`WebsiteUrl`) and the resolved feed XML URL (`FeedUrl`).

### 2. YouTube Channel Feed Ingestion
YouTube publishes RSS feeds for all channels at:
`https://www.youtube.com/feeds/videos.xml?channel_id={channelId}`

If a user submits a channel handle or URL (e.g., `https://www.youtube.com/@ChannelName` or `https://www.youtube.com/channel/UC...`):
- We will resolve the channel ID using auto-discovery/HTML scraping (as parsed inside the existing `WebScraperService.FetchYouTubeMetadataAsync` matching `youtube.com/feeds/videos.xml?channel_id=(UC[\w-]+)`).
- Once registered, the Polling Engine will ingest new video links.
- Because of **Smart Source Detection (Section A)**, the engine recognizes the YouTube URL, automatically extracts its transcript via `YouTubeService`, and hands the full transcript to the LLM agent to compose a draft thread.

---

## Database Schema (Postgres)
```sql
CREATE TABLE FeedSubscriptions (
    Id UUID PRIMARY KEY,
    UserId UUID NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    Title VARCHAR(200) NOT NULL,
    FeedUrl VARCHAR(500) NOT NULL,
    WebsiteUrl VARCHAR(500),
    InstructionPrompt TEXT NOT NULL,
    AutoPublish BOOLEAN NOT NULL DEFAULT FALSE,
    IncludeFilters VARCHAR(500),
    ExcludeFilters VARCHAR(500),
    LastPolledAt TIMESTAMP WITH TIME ZONE,
    CreatedAt TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

CREATE INDEX IX_FeedSubscriptions_UserId ON FeedSubscriptions(UserId);
```
